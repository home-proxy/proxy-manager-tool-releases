namespace NetAgent.ProxyManager.Core.Models;

public sealed class TransactionHistoryItem
{
    public string Id { get; init; } = string.Empty;
    public string Code { get; init; } = string.Empty;
    public int? Type { get; init; }
    public decimal Amount { get; init; }
    public string Content { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string OrderCode { get; init; } = string.Empty;
    public int? StatusId { get; init; }
    public string StatusName { get; init; } = string.Empty;
    public DateTimeOffset? CreatedAt { get; init; }
}
