using System.IO.Compression;
using System.Net;
using System.Security.Cryptography;
using FluentAssertions;
using Microsoft.Extensions.Options;
using NetAgent.ProxyManager.Infrastructure.Api;
using NetAgent.ProxyManager.Infrastructure.Services;

namespace NetAgent.ProxyManager.Tests;

public sealed class ChromeBrowserProvisionerTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "netagent-browser-tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task EnsureInstalledAsync_ShouldDownloadVerifyAndExtractPinnedVersion()
    {
        var zip = CreateBrowserZip();
        var handler = new CountingHandler(zip);
        var provisioner = CreateProvisioner(handler, Convert.ToHexString(SHA256.HashData(zip)));

        provisioner.IsInstalled.Should().BeFalse();
        var path = await provisioner.EnsureInstalledAsync(progress: null, CancellationToken.None);

        path.Should().Be(Path.Combine(_root, "1.0.0", "chrome-win64", "chrome.exe"));
        File.Exists(path).Should().BeTrue();
        provisioner.IsInstalled.Should().BeTrue();
        provisioner.GetInstalledExecutablePaths().Should().Equal(path);
        Directory.EnumerateFileSystemEntries(_root).Select(Path.GetFileName).Should().Equal("1.0.0");

        await provisioner.EnsureInstalledAsync(progress: null, CancellationToken.None);
        handler.Requests.Should().Be(1, "an installed browser is not downloaded again");
    }

    [Fact]
    public async Task EnsureInstalledAsync_ShouldRefuseAndCleanUpWhenHashDoesNotMatch()
    {
        var provisioner = CreateProvisioner(new CountingHandler(CreateBrowserZip()), new string('a', 64));

        var act = () => provisioner.EnsureInstalledAsync(progress: null, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*SHA-256*");
        provisioner.IsInstalled.Should().BeFalse();
        Directory.EnumerateFileSystemEntries(_root).Should().BeEmpty("no partial download or extraction may be left behind");
    }

    [Fact]
    public async Task EnsureInstalledAsync_ShouldRequireConfiguredHash()
    {
        var handler = new CountingHandler(CreateBrowserZip());
        var provisioner = CreateProvisioner(handler, sha256: string.Empty);

        var act = () => provisioner.EnsureInstalledAsync(progress: null, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*Sha256*");
        handler.Requests.Should().Be(0);
    }

    [Fact]
    public async Task EnsureInstalledAsync_ShouldRemoveOlderVersionsNotInUse()
    {
        var oldExe = Path.Combine(_root, "0.9.0", "chrome-win64", "chrome.exe");
        Directory.CreateDirectory(Path.GetDirectoryName(oldExe)!);
        File.WriteAllText(oldExe, "old");
        var zip = CreateBrowserZip();
        var provisioner = CreateProvisioner(new CountingHandler(zip), Convert.ToHexString(SHA256.HashData(zip)));
        provisioner.GetInstalledExecutablePaths().Should().ContainSingle()
            .Which.Should().Be(oldExe, "the leak guard must also cover older installed versions");

        await provisioner.EnsureInstalledAsync(progress: null, CancellationToken.None);

        Directory.Exists(Path.Combine(_root, "0.9.0")).Should().BeFalse();
    }

    private ChromeBrowserProvisioner CreateProvisioner(HttpMessageHandler handler, string sha256) =>
        new(new SingleClientFactory(handler), Options.Create(new ChromeBrowserOptions
        {
            Version = "1.0.0",
            DownloadUrl = "https://example.test/chrome-win64.zip",
            Sha256 = sha256,
            InstallDirectory = _root
        }));

    private static byte[] CreateBrowserZip()
    {
        using var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            using var writer = new StreamWriter(archive.CreateEntry("chrome-win64/chrome.exe").Open());
            writer.Write("fake chrome");
        }

        return stream.ToArray();
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private sealed class CountingHandler(byte[] payload) : HttpMessageHandler
    {
        public int Requests { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(payload) });
        }
    }

    private sealed class SingleClientFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
    }
}
