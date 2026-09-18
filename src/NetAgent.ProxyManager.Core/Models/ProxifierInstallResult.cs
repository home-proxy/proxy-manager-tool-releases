namespace NetAgent.ProxyManager.Core.Models;

public sealed record ProxifierInstallResult(
    bool Success,
    string Message,
    string? ProxifierExecutablePath = null,
    int? ExitCode = null,
    string? InstallerPath = null,
    string? LogPath = null);
