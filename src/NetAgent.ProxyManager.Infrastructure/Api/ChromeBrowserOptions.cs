namespace NetAgent.ProxyManager.Infrastructure.Api;

// Pinned build of the tool-owned browser (Chrome for Testing). Bumping Version requires updating
// DownloadUrl and Sha256 together — see docs/09-chrome-profiles.md.
public sealed class ChromeBrowserOptions
{
    public string Version { get; set; } = string.Empty;
    public string DownloadUrl { get; set; } = string.Empty;
    public string Sha256 { get; set; } = string.Empty;
    public string InstallDirectory { get; set; } = "%AppData%\\ProxyManager\\browser";
    public string ExecutableRelativePath { get; set; } = "chrome-win64\\chrome.exe";
    public int DownloadTimeoutSeconds { get; set; } = 1800;
}
