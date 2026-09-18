namespace NetAgent.ProxyManager.Core.Models;

public sealed class PurchaseHistoryItem
{
    public string Id { get; init; } = string.Empty;
    public string Code { get; init; } = string.Empty;
    public string Account { get; init; } = string.Empty;
    public int Quantity { get; init; }
    public decimal Amount { get; init; }
    public string DayOfUse { get; init; } = string.Empty;
    public string TaxCode { get; init; } = string.Empty;
    public int? StatusId { get; init; }
    public string StatusName { get; init; } = string.Empty;
    public DateTimeOffset? CreatedAt { get; init; }
}
