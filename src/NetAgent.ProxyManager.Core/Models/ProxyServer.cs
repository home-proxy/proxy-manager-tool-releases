namespace NetAgent.ProxyManager.Core.Models;

public sealed class ProxyServer
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Proxy { get; set; } = string.Empty;
    public ProxyProtocol Protocol { get; set; } = ProxyProtocol.Socks5;
    public string? Username { get; set; }
    public string? Password { get; set; }
    public ProxyStatus Status { get; set; } = ProxyStatus.Unknown;
    public int? LatencyMs { get; set; }
    public bool IsFromBackend { get; set; }
    public int? BackendUserProxyId { get; set; }
    public ProxyOrderKind? BackendOrderKind { get; set; }
    public string? BackendProvider { get; set; }
    public DateTimeOffset? ExpiredAt { get; set; }

    public string Host => TryGetEndpoint(out var host, out _) ? host : string.Empty;
    public int Port => TryGetEndpoint(out _, out var port) ? port : 0;
    public bool HasValidEndpoint => TryGetEndpoint(out _, out _);
    public string DisplayValue
    {
        get
        {
            var protocol = Protocol.ToDisplayToken();
            return string.IsNullOrWhiteSpace(Username)
                ? $"{Proxy}:{protocol}"
                : $"{Proxy}:{Username}:{Password}:{protocol}";
        }
    }

    public bool TryGetEndpoint(out string host, out int port)
    {
        host = string.Empty;
        port = default;

        var segments = Proxy.Split(':', StringSplitOptions.TrimEntries);
        if (segments.Length != 2 ||
            string.IsNullOrWhiteSpace(segments[0]) ||
            !int.TryParse(segments[1], out port) ||
            port is <= 0 or > 65535)
        {
            return false;
        }

        host = segments[0];
        return true;
    }
}
