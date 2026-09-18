namespace NetAgent.ProxyManager.Core.Models;

public sealed class HistoryPageRequest
{
    public int Page { get; init; } = 1;
    public int Limit { get; init; } = 10;
    public string SearchField { get; init; } = HistorySearchFields.All;
    public string? SearchText { get; init; }
    public HistoryStatusFilter Status { get; init; } = HistoryStatusFilter.All;
}
