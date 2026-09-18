namespace NetAgent.ProxyManager.Core.Models;

public sealed class ProxyOrderPage
{
    public IReadOnlyList<ProxyOrder> Orders { get; init; } = [];
    public int? Total { get; init; }
    public bool HasNextPage { get; init; }
}
