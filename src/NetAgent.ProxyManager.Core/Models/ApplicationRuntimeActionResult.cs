namespace NetAgent.ProxyManager.Core.Models;

public sealed record ApplicationRuntimeActionResult(
    bool Success,
    string Message,
    int AffectedProcessCount = 0);
