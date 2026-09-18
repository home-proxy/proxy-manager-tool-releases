using System.Text.RegularExpressions;
using NetAgent.ProxyManager.Core.Interfaces;

namespace NetAgent.ProxyManager.Core.Services;

public sealed partial class LoggerSanitizer : ILoggerSanitizer
{
    [GeneratedRegex(@"(?i)\b(api[-_ ]?key|apikey|access[-_ ]?token|refresh[-_ ]?token|token|bearer|secret[-_ ]?key)\b\s*[:=]\s*([^\s,;]+)")]
    private static partial Regex NamedSecretRegex();

    [GeneratedRegex("(?i)(\"(?:accessToken|refreshToken|token|secretKey)\"\\s*:\\s*\")([^\"]+)(\")")]
    private static partial Regex JsonSecretRegex();

    [GeneratedRegex(@"(?i)\bBearer\s+([A-Za-z0-9\-._~+/]+=*)")]
    private static partial Regex BearerRegex();

    [GeneratedRegex(@"(?<scheme>\b(?:socks5|socks4|https?|ftp)://)?(?<user>[^:\s/@]+):(?<password>[^@\s]+)@(?<host>[^:\s/]+):(?<port>\d{2,5})")]
    private static partial Regex ProxyUriRegex();

    public string Sanitize(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return message;
        }

        var sanitized = NamedSecretRegex().Replace(message, match => $"{match.Groups[1].Value}=****");
        sanitized = JsonSecretRegex().Replace(sanitized, "$1****$3");
        sanitized = BearerRegex().Replace(sanitized, "Bearer ****");
        sanitized = ProxyUriRegex().Replace(
            sanitized,
            match => $"{match.Groups["scheme"].Value}{match.Groups["user"].Value}:****@{match.Groups["host"].Value}:{match.Groups["port"].Value}");

        return sanitized;
    }
}
