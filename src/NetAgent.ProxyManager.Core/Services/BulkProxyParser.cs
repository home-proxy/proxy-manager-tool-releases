using NetAgent.ProxyManager.Core.Interfaces;
using NetAgent.ProxyManager.Core.Models;

namespace NetAgent.ProxyManager.Core.Services;

public sealed class BulkProxyParser : IBulkProxyParser
{
    public ProxyImportResult Parse(string input)
    {
        var proxies = new List<ProxyServer>();
        var errors = new List<ProxyImportError>();
        var lines = input.Replace("\r\n", "\n").Split('\n');

        for (var index = 0; index < lines.Length; index++)
        {
            var rawLine = lines[index].Trim();
            if (string.IsNullOrWhiteSpace(rawLine))
            {
                continue;
            }

            if (TryParseLine(rawLine, out var proxy, out var error))
            {
                proxies.Add(proxy!);
            }
            else
            {
                errors.Add(new ProxyImportError(index + 1, rawLine, error!));
            }
        }

        return new ProxyImportResult(proxies, errors);
    }

    private static bool TryParseLine(string line, out ProxyServer? proxy, out string? error)
    {
        proxy = null;
        error = null;

        if (TryParseUriLine(line, out proxy, out error))
        {
            return true;
        }

        if (error is not null)
        {
            return false;
        }

        var segments = line.Split(':', StringSplitOptions.TrimEntries);
        if (segments.Length is not (2 or 3 or 4 or 5))
        {
            error = "Định dạng hợp lệ: ip:port, ip:port:user:pass, ip:port:protocol hoặc ip:port:user:pass:protocol.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(segments[0]) || !TryParsePort(segments[1], out _))
        {
            error = "Địa chỉ proxy hoặc port không hợp lệ.";
            return false;
        }

        var protocol = ProxyProtocol.Socks5;
        var hasExplicitProtocol = segments.Length is 3 or 5;
        if (hasExplicitProtocol && !ProxyProtocolDisplay.TryParse(segments[^1], out protocol))
        {
            error = "Protocol chỉ hỗ trợ HTTP hoặc SOCKS5.";
            return false;
        }

        proxy = new ProxyServer
        {
            Proxy = $"{segments[0]}:{segments[1]}",
            Username = segments.Length >= 4 ? NormalizeOptional(segments[2]) : null,
            Password = segments.Length >= 4 ? NormalizeOptional(segments[3]) : null,
            Protocol = protocol
        };
        return true;
    }

    private static bool TryParseUriLine(string line, out ProxyServer? proxy, out string? error)
    {
        proxy = null;
        error = null;

        if (!line.Contains("://", StringComparison.Ordinal))
        {
            return false;
        }

        if (!Uri.TryCreate(line, UriKind.Absolute, out var uri))
        {
            error = "URI proxy không hợp lệ.";
            return false;
        }

        if (!ProxyProtocolDisplay.TryParse(uri.Scheme, out var protocol))
        {
            error = "Protocol chỉ hỗ trợ HTTP hoặc SOCKS5.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(uri.Host) ||
            !HasExplicitPort(uri) ||
            !TryParsePort(uri.Port.ToString(), out _))
        {
            error = "Địa chỉ proxy hoặc port không hợp lệ.";
            return false;
        }

        string? username = null;
        string? password = null;
        if (!string.IsNullOrWhiteSpace(uri.UserInfo))
        {
            var userInfo = uri.UserInfo.Split(':', 2, StringSplitOptions.TrimEntries);
            username = NormalizeOptional(Uri.UnescapeDataString(userInfo[0]));
            password = userInfo.Length > 1 ? NormalizeOptional(Uri.UnescapeDataString(userInfo[1])) : null;
        }

        proxy = new ProxyServer
        {
            Proxy = $"{uri.Host}:{uri.Port}",
            Username = username,
            Password = password,
            Protocol = protocol
        };
        return true;
    }

    private static bool HasExplicitPort(Uri uri)
    {
        var authority = uri.Authority;
        var hostPort = authority.Contains('@', StringComparison.Ordinal)
            ? authority[(authority.LastIndexOf('@') + 1)..]
            : authority;
        return hostPort.Contains(':', StringComparison.Ordinal);
    }

    private static string? NormalizeOptional(string value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static bool TryParsePort(string value, out int port) =>
        int.TryParse(value, out port) && port is > 0 and <= 65535;

}
