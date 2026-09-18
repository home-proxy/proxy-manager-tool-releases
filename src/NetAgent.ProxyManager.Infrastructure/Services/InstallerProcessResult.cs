namespace NetAgent.ProxyManager.Infrastructure.Services;

public sealed record InstallerProcessResult(bool Started, int? ExitCode, string? ErrorMessage = null);
