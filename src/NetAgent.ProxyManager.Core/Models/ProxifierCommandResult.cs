namespace NetAgent.ProxyManager.Core.Models;

public sealed record ProxifierCommandResult(bool Success, string Message, int? ExitCode = null);
