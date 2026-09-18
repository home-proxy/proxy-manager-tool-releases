namespace NetAgent.ProxyManager.Core.Models;

public enum ProxyProtocol
{
    Socks5,
    Https,
}

public static class ProxyProtocolDisplay
{
    public static IReadOnlyList<ProxyProtocolOption> Options { get; } =
    [
        new(ProxyProtocol.Socks5, "SOCKS5"),
        new(ProxyProtocol.Https, "HTTP")
    ];

    public static string ToDisplayName(this ProxyProtocol protocol) =>
        protocol switch
        {
            ProxyProtocol.Socks5 => "SOCKS5",
            ProxyProtocol.Https => "HTTP",
            _ => protocol.ToString()
        };

    public static string ToDisplayToken(this ProxyProtocol protocol) =>
        protocol switch
        {
            ProxyProtocol.Socks5 => "socks5",
            ProxyProtocol.Https => "http",
            _ => protocol.ToString().ToLowerInvariant()
        };

    public static string ToProxifierProfileType(this ProxyProtocol protocol) =>
        protocol switch
        {
            ProxyProtocol.Socks5 => "SOCKS5",
            ProxyProtocol.Https => "HTTPS",
            _ => throw new ArgumentOutOfRangeException(nameof(protocol), protocol, "Unsupported proxy protocol.")
        };

    public static bool TryParse(string value, out ProxyProtocol protocol)
    {
        if (string.Equals(value, "SOCKS", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(value, "SOCKS5", StringComparison.OrdinalIgnoreCase))
        {
            protocol = ProxyProtocol.Socks5;
            return true;
        }

        if (string.Equals(value, "HTTP", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(value, "HTTPS", StringComparison.OrdinalIgnoreCase))
        {
            protocol = ProxyProtocol.Https;
            return true;
        }

        protocol = default;
        return false;
    }
}

public sealed record ProxyProtocolOption(ProxyProtocol Value, string DisplayName);
