namespace NetAgent.ProxyManager.Core.Models;

public sealed class DepositTransaction
{
    public string Id { get; init; } = string.Empty;
    public long Amount { get; init; }
    public string AccountName { get; init; } = string.Empty;
    public string AccountNumber { get; init; } = string.Empty;
    public string BankName { get; init; } = string.Empty;
    public string BankCode { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string QrCode { get; init; } = string.Empty;
    public int? StatusId { get; init; }
    public string StatusName { get; init; } = string.Empty;
    public DateTimeOffset? ExpiredAt { get; init; }
    public DateTimeOffset? ExpiresAt { get; init; }

    public bool IsPaid => StatusId == 6;

    public bool IsCancelled => StatusId == 7;
}
