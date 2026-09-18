namespace NetAgent.ProxyManager.Core.Models;

public sealed class HistoryPage<T>
{
    public IReadOnlyList<T> Items { get; init; } = [];
    public int? Total { get; init; }
    public bool HasNextPage { get; init; }
}
