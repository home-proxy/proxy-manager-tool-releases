using System.Text.Json.Serialization;

namespace NetAgent.ProxyManager.Core.Models;

public sealed class ProxyPurchaseOrder
{
    public string PaymentMethod { get; init; } = "WALLET";
    public IReadOnlyList<ProxyPurchaseProduct> Products { get; init; } = [];
}

public sealed class ProxyPurchaseProduct
{
    public bool IsCdk { get; init; }
    public int DayOfUse { get; init; }
    public int RotateInterval { get; init; }
    public string Password { get; init; } = string.Empty;

    [JsonPropertyName("user")]
    public string User { get; init; } = string.Empty;

    public string ProtocolType { get; init; } = "HTTP";
    public string Provider { get; init; } = string.Empty;
    public int Quantity { get; init; } = 1;
    public bool? IsAutoRotate { get; init; }
    public ProxyPurchaseProductReference Product { get; init; } = new();
}

public sealed class ProxyPurchaseProductReference
{
    public string Id { get; init; } = string.Empty;
}
