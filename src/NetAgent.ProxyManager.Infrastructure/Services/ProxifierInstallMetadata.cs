namespace NetAgent.ProxyManager.Infrastructure.Services;

public sealed record ProxifierInstallMetadata(
    string? ProxifierExecutablePath,
    string? UninstallString);
