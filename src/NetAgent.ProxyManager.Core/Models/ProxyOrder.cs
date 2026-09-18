namespace NetAgent.ProxyManager.Core.Models;

public sealed class ProxyOrder
{
    public int UserProxyId { get; init; }
    public string Code { get; init; } = string.Empty;
    public string OrderCode { get; init; } = string.Empty;
    public string Provider { get; init; } = string.Empty;
    public string Ip { get; init; } = string.Empty;
    public int Port { get; init; }
    public string Domain { get; init; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public ProxyProtocol Protocol { get; set; } = ProxyProtocol.Https;
    public string? PreviousIp { get; init; }
    public string? Description { get; init; }
    public DateTimeOffset? ExpiredAt { get; init; }
    public int CategoryTypeId { get; init; }
    public int? StatusId { get; init; }
    public string StatusName { get; init; } = string.Empty;
    public bool IsCdk { get; init; }
    public int RotateInterval { get; init; }

    public bool IsDatacenter =>
        string.Equals(Provider, "CMC", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(Provider, "US", StringComparison.OrdinalIgnoreCase);

    public string ProxyAddress => string.IsNullOrWhiteSpace(Ip)
        ? string.Empty
        : $"{Ip}:{Port}:{Username}:{Password}";

    public string ProxyDomain => string.IsNullOrWhiteSpace(Domain)
        ? string.Empty
        : $"{Domain}:{Port}:{Username}:{Password}";
}
