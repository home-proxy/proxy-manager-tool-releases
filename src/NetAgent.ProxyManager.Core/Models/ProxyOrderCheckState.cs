namespace NetAgent.ProxyManager.Core.Models;

public sealed record ProxyOrderCheckState(ProxyStatus Status, int? LatencyMs);
