namespace NetAgent.ProxyManager.Core.Models;

public sealed record ProxyCheckResult(
    bool IsReachable,
    int? LatencyMs,
    string? ErrorMessage = null,
    string? EgressIp = null);
