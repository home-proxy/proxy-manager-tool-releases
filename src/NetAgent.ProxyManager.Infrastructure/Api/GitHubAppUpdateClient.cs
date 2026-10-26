using NetAgent.ProxyManager.Core.Models;
using NetAgent.ProxyManager.Core.Services;

namespace NetAgent.ProxyManager.Infrastructure.Api;

public sealed class GitHubAppUpdateClient(HttpClient httpClient)
{
    public const string RepositoryPath = "repos/home-proxy/proxy-manager-tool-releases/releases/latest";
    private static readonly TimeSpan CheckTimeout = TimeSpan.FromSeconds(15);

    public async Task<AppRelease?> GetLatestAsync(CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(CheckTimeout);
        using var request = new HttpRequestMessage(HttpMethod.Get, RepositoryPath);
        request.Headers.Accept.ParseAdd("application/vnd.github+json");
        using var response = await httpClient.SendAsync(request, timeout.Token);
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync(timeout.Token);
        return GitHubReleaseParser.TryParseLatest(json);
    }

    public async Task DownloadAsync(
        AppRelease release,
        string destinationPath,
        IProgress<AppUpdateDownloadProgress>? progress,
        CancellationToken cancellationToken)
    {
        if (!GitHubReleaseParser.IsTrustedDownloadUri(release.DownloadUri))
        {
            throw new InvalidOperationException("Địa chỉ tải bản cập nhật không hợp lệ.");
        }

        using var request = new HttpRequestMessage(HttpMethod.Get, release.DownloadUri);
        using var response = await httpClient.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);
        response.EnsureSuccessStatusCode();

        var directory = Path.GetDirectoryName(destinationPath)
            ?? throw new InvalidOperationException("Không tạo được thư mục tải bản cập nhật.");
        Directory.CreateDirectory(directory);
        var partialPath = destinationPath + ".partial";
        var total = response.Content.Headers.ContentLength ?? release.Size;

        await using (var input = await response.Content.ReadAsStreamAsync(cancellationToken))
        await using (var output = new FileStream(partialPath, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            var buffer = new byte[81920];
            long received = 0;
            int read;
            while ((read = await input.ReadAsync(buffer, cancellationToken)) > 0)
            {
                await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                received += read;
                progress?.Report(new AppUpdateDownloadProgress(received, total));
            }
        }

        var downloaded = new FileInfo(partialPath);
        if (total is > 0 && downloaded.Length != total)
        {
            downloaded.Delete();
            throw new IOException("Tệp tải về không đầy đủ.");
        }

        if (File.Exists(destinationPath))
        {
            File.Delete(destinationPath);
        }

        File.Move(partialPath, destinationPath);
    }
}
