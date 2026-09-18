namespace NetAgent.ProxyManager.Core.Models;

public sealed class ProxySessionDto
{
    public string SessionId { get; init; } = string.Empty;
    public string CountryCode { get; init; } = string.Empty;
    public DateTimeOffset ExpiresAt { get; init; }
    public IReadOnlyList<ProxyServer> Proxies { get; init; } = Array.Empty<ProxyServer>();
}
