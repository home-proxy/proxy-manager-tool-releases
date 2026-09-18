namespace NetAgent.ProxyManager.Core.Models;

public sealed class ProxyOrderPageRequest
{
    public int Page { get; init; } = 1;
    public int Limit { get; init; } = 20;
    public int CategoryTypeId { get; init; } = 1;
    public bool? IsCdk { get; init; }
    public ProxyOrderKind? OrderKind { get; init; }
    public IReadOnlyList<string>? ProviderIn { get; init; }
    public ProxyOrderSearchField? SearchField { get; init; }
    public string? SearchText { get; init; }
}
