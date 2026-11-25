using NetAgent.ProxyManager.Core.Models;

namespace NetAgent.ProxyManager.Core.Services;

public enum SystemProxyState
{
    Disabled,
    Usable,
    Unusable
}

public sealed record SystemProxyEligibilityResult(SystemProxyState State, ProxyServer? Proxy, string Message)
{
    public bool IsUsable => State == SystemProxyState.Usable;
}

// The whole-PC proxy (AppSettings.SystemProxyId) is held to the same standard as a proxy assigned to
// an application rule: it must exist, have a valid endpoint and not be expired.
public static class SystemProxyEligibility
{
    public const string MissingProxyMessage = "Proxy gắn cho toàn bộ máy không còn tồn tại.";
    public const string InvalidEndpointMessage = "Proxy gắn cho toàn bộ máy chưa có endpoint hợp lệ.";
    public const string ExpiredProxyMessage = "Proxy gắn cho toàn bộ máy đã hết hạn.";

    public static SystemProxyEligibilityResult Evaluate(
        Guid? systemProxyId,
        IEnumerable<ProxyServer> proxies,
        DateTimeOffset now)
    {
        if (systemProxyId is null)
        {
            return new SystemProxyEligibilityResult(SystemProxyState.Disabled, null, string.Empty);
        }

        var proxy = proxies.FirstOrDefault(candidate => candidate.Id == systemProxyId.Value);
        if (proxy is null)
        {
            return new SystemProxyEligibilityResult(SystemProxyState.Unusable, null, MissingProxyMessage);
        }

        if (!proxy.HasValidEndpoint)
        {
            return new SystemProxyEligibilityResult(SystemProxyState.Unusable, proxy, InvalidEndpointMessage);
        }

        if (proxy.ExpiredAt is not null && proxy.ExpiredAt.Value <= now)
        {
            return new SystemProxyEligibilityResult(SystemProxyState.Unusable, proxy, ExpiredProxyMessage);
        }

        return new SystemProxyEligibilityResult(SystemProxyState.Usable, proxy, string.Empty);
    }

    // The single place that turns the setting into the profile's default route. An unusable whole-PC
    // proxy blocks the default route instead of falling back to Direct: someone who put the whole
    // machine behind a proxy must not be silently moved back onto their real IP.
    public static (Guid? DefaultProxyId, bool DefaultRouteDirect) ResolveDefaultRoute(
        AppSettings settings,
        IEnumerable<ProxyServer> proxies,
        DateTimeOffset now)
    {
        var result = Evaluate(settings.SystemProxyId, proxies, now);
        return result.State switch
        {
            SystemProxyState.Usable => (settings.SystemProxyId, settings.DefaultRouteDirect),
            SystemProxyState.Unusable => (null, false),
            _ => (null, settings.DefaultRouteDirect)
        };
    }
}
