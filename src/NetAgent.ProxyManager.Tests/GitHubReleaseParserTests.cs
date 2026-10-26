using FluentAssertions;
using NetAgent.ProxyManager.Core.Models;
using NetAgent.ProxyManager.Core.Services;

namespace NetAgent.ProxyManager.Tests;

public sealed class GitHubReleaseParserTests
{
    [Theory]
    [InlineData("v1.3.0", "1.3.0")]
    [InlineData("1.2.1+abc", "1.2.1")]
    [InlineData("V2.0.0-beta.1", "2.0.0")]
    public void TryParse_ShouldReadReleaseTags(string text, string expected)
    {
        AppVersion.TryParse(text, out var version).Should().BeTrue();
        version.ToDisplayString().Should().Be(expected);
    }

    [Fact]
    public void CompareTo_ShouldOrderNumericVersions()
    {
        AppVersion.TryParse("1.3.0", out var older);
        AppVersion.TryParse("v1.3.1", out var newer);

        newer.CompareTo(older).Should().BeGreaterThan(0);
        older.CompareTo(older).Should().Be(0);
    }

    [Fact]
    public void TryParseLatest_ShouldReturnTheProxyManagerAsset()
    {
        const string json = """
            {
              "tag_name": "v1.4.0",
              "assets": [
                { "name": "notes.txt", "browser_download_url": "https://github.com/home-proxy/proxy-manager-tool-releases/releases/download/v1.4.0/notes.txt", "size": 12 },
                { "name": "ProxyManager.App.exe", "browser_download_url": "https://github.com/home-proxy/proxy-manager-tool-releases/releases/download/v1.4.0/ProxyManager.App.exe", "size": 80000000 }
              ]
            }
            """;

        var release = GitHubReleaseParser.TryParseLatest(json);

        release.Should().NotBeNull();
        release!.Version.ToDisplayString().Should().Be("1.4.0");
        release.Size.Should().Be(80_000_000);
        release.DownloadUri.Host.Should().Be("github.com");
    }

    [Theory]
    [InlineData("http://github.com/home-proxy/proxy-manager-tool-releases/releases/download/v1.4.0/ProxyManager.App.exe")]
    [InlineData("https://example.com/ProxyManager.App.exe")]
    public void TryParseLatest_ShouldRejectUntrustedDownloadUrls(string url)
    {
        var json = $$"""
            {
              "tag_name": "v1.4.0",
              "assets": [
                { "name": "ProxyManager.App.exe", "browser_download_url": "{{url}}", "size": 80000000 }
              ]
            }
            """;

        GitHubReleaseParser.TryParseLatest(json).Should().BeNull();
    }
}
