using System.IO.Compression;
using Microsoft.Extensions.Options;
using NetAgent.ProxyManager.Core.Interfaces;
using NetAgent.ProxyManager.Core.Models;
using NetAgent.ProxyManager.Infrastructure.Api;

namespace NetAgent.ProxyManager.Infrastructure.Services;

// Downloads the pinned Chrome for Testing zip on first use, verifies its SHA-256 (same policy as
// the bundled ProxifierSetup.exe: no configured hash, no install) and extracts it to
// <InstallDirectory>\<Version>. The extracted folder only appears once complete, so a crash
// mid-download never leaves a half-installed browser that looks usable.
public sealed class ChromeBrowserProvisioner : IChromeBrowserProvisioner
{
    public const string HttpClientName = "ChromeBrowserDownload";
    private const int CopyBufferSize = 81920;

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ChromeBrowserOptions _options;
    private readonly string _installDirectory;
    private readonly SemaphoreSlim _installLock = new(1, 1);

    public ChromeBrowserProvisioner(IHttpClientFactory httpClientFactory, IOptions<ChromeBrowserOptions> options)
    {
        _httpClientFactory = httpClientFactory;
        _options = options.Value;
        _installDirectory = Path.GetFullPath(Environment.ExpandEnvironmentVariables(_options.InstallDirectory));
    }

    public string BrowserExecutablePath => Path.Combine(GetVersionDirectory(), _options.ExecutableRelativePath);

    public bool IsInstalled => !string.IsNullOrWhiteSpace(_options.Version) && File.Exists(BrowserExecutablePath);

    public IReadOnlyList<string> GetInstalledExecutablePaths()
    {
        if (!Directory.Exists(_installDirectory))
        {
            return [];
        }

        return Directory.EnumerateDirectories(_installDirectory)
            .Where(directory => !Path.GetFileName(directory).StartsWith('.'))
            .Select(directory => Path.Combine(directory, _options.ExecutableRelativePath))
            .Where(File.Exists)
            .ToList();
    }

    public async Task<string> EnsureInstalledAsync(
        IProgress<ChromeBrowserDownloadProgress>? progress,
        CancellationToken cancellationToken)
    {
        if (IsInstalled)
        {
            return BrowserExecutablePath;
        }

        ValidateOptions();
        await _installLock.WaitAsync(cancellationToken);
        try
        {
            if (IsInstalled)
            {
                return BrowserExecutablePath;
            }

            Directory.CreateDirectory(_installDirectory);
            var zipPath = Path.Combine(_installDirectory, $".{_options.Version}.zip.part");
            var extractDirectory = Path.Combine(_installDirectory, $".{_options.Version}.extracting");
            try
            {
                await DownloadAsync(zipPath, progress, cancellationToken);

                progress?.Report(new ChromeBrowserDownloadProgress("Đang kiểm tra file tải về...", 0, null));
                var actualHash = await ProxifierInstallerService.ComputeSha256Async(zipPath, cancellationToken);
                if (!string.Equals(actualHash, ProxifierInstallerService.NormalizeHash(_options.Sha256 ?? string.Empty), StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException(
                        "File trình duyệt tải về không khớp SHA-256 đã cấu hình. Đã huỷ cài đặt để đảm bảo an toàn.");
                }

                progress?.Report(new ChromeBrowserDownloadProgress("Đang giải nén trình duyệt...", 0, null));
                DeleteDirectoryIfExists(extractDirectory);
                await Task.Run(() => ZipFile.ExtractToDirectory(zipPath, extractDirectory), cancellationToken);
                if (!File.Exists(Path.Combine(extractDirectory, _options.ExecutableRelativePath)))
                {
                    throw new InvalidOperationException("Gói trình duyệt không chứa " + _options.ExecutableRelativePath + ".");
                }

                DeleteDirectoryIfExists(GetVersionDirectory());
                Directory.Move(extractDirectory, GetVersionDirectory());
            }
            finally
            {
                TryDeleteFile(zipPath);
                TryDeleteDirectory(extractDirectory);
            }

            RemoveOtherVersions();
            return BrowserExecutablePath;
        }
        finally
        {
            _installLock.Release();
        }
    }

    private async Task DownloadAsync(
        string zipPath,
        IProgress<ChromeBrowserDownloadProgress>? progress,
        CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(Math.Max(60, _options.DownloadTimeoutSeconds)));

        var client = _httpClientFactory.CreateClient(HttpClientName);
        using var response = await client.GetAsync(_options.DownloadUrl, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
        response.EnsureSuccessStatusCode();
        var totalBytes = response.Content.Headers.ContentLength;

        await using var source = await response.Content.ReadAsStreamAsync(timeout.Token);
        await using var target = File.Create(zipPath);
        var buffer = new byte[CopyBufferSize];
        long received = 0;
        var lastReportedPercent = -1;
        int read;
        while ((read = await source.ReadAsync(buffer, timeout.Token)) > 0)
        {
            await target.WriteAsync(buffer.AsMemory(0, read), timeout.Token);
            received += read;
            var report = new ChromeBrowserDownloadProgress("Đang tải trình duyệt...", received, totalBytes);
            if (report.Percent != lastReportedPercent)
            {
                lastReportedPercent = report.Percent ?? -1;
                progress?.Report(report);
            }
        }
    }

    private void ValidateOptions()
    {
        if (string.IsNullOrWhiteSpace(_options.Version) || string.IsNullOrWhiteSpace(_options.DownloadUrl))
        {
            throw new InvalidOperationException("Chưa cấu hình ChromeBrowser:Version/DownloadUrl.");
        }

        if (ProxifierInstallerService.NormalizeHash(_options.Sha256 ?? string.Empty).Length != 64)
        {
            throw new InvalidOperationException("Chưa cấu hình ChromeBrowser:Sha256 cho gói trình duyệt.");
        }

        if (_options.Version.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || _options.Version.StartsWith('.'))
        {
            throw new InvalidOperationException("ChromeBrowser:Version không hợp lệ.");
        }
    }

    // Older versions are removed best-effort. A version still used by a running profile cannot be
    // renamed (Windows refuses while its files are open), so it is left intact — never half-deleted
    // under a live browser — and stays covered by the leak guard until a later install removes it.
    private void RemoveOtherVersions()
    {
        foreach (var directory in Directory.EnumerateDirectories(_installDirectory))
        {
            var name = Path.GetFileName(directory);
            if (name.StartsWith('.') || string.Equals(name, _options.Version, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var retiredDirectory = Path.Combine(_installDirectory, $".{name}.retired");
            try
            {
                Directory.Move(directory, retiredDirectory);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                continue;
            }

            TryDeleteDirectory(retiredDirectory);
        }
    }

    private string GetVersionDirectory() => Path.Combine(_installDirectory, _options.Version);

    private static void DeleteDirectoryIfExists(string directory)
    {
        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static void TryDeleteDirectory(string directory)
    {
        try
        {
            DeleteDirectoryIfExists(directory);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }

    private static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }
}
