using System.Text.Json;
using NetAgent.ProxyManager.Core.Models;

namespace NetAgent.ProxyManager.Core.Services;

public static class GitHubReleaseParser
{
    public const string AssetName = "ProxyManager.App.exe";

    public static AppRelease? TryParseLatest(string json, string assetName = AssetName)
    {
        if (string.IsNullOrWhiteSpace(json) || string.IsNullOrWhiteSpace(assetName))
        {
            return null;
        }

        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        if (!root.TryGetProperty("tag_name", out var tagElement) ||
            tagElement.GetString() is not { } tag ||
            !AppVersion.TryParse(tag, out var version) ||
            !root.TryGetProperty("assets", out var assets) ||
            assets.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        foreach (var asset in assets.EnumerateArray())
        {
            if (!asset.TryGetProperty("name", out var nameElement) ||
                !string.Equals(nameElement.GetString(), assetName, StringComparison.OrdinalIgnoreCase) ||
                !asset.TryGetProperty("browser_download_url", out var urlElement) ||
                urlElement.GetString() is not { } url ||
                !Uri.TryCreate(url, UriKind.Absolute, out var downloadUri) ||
                !IsTrustedDownloadUri(downloadUri))
            {
                continue;
            }

            long? size = asset.TryGetProperty("size", out var sizeElement) && sizeElement.TryGetInt64(out var bytes)
                ? bytes
                : null;
            return new AppRelease(version, tag, downloadUri, assetName, size);
        }

        return null;
    }

    public static bool IsTrustedDownloadUri(Uri uri) =>
        uri.IsAbsoluteUri &&
        uri.Scheme == Uri.UriSchemeHttps &&
        (uri.Host.Equals("github.com", StringComparison.OrdinalIgnoreCase) ||
         uri.Host.EndsWith(".githubusercontent.com", StringComparison.OrdinalIgnoreCase));
}
