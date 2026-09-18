using NetAgent.ProxyManager.Core.Models;

namespace NetAgent.ProxyManager.Core.Services;

public static class BackendProxyOrderSource
{
    public const int DefaultPageLimit = 100;

    public static readonly ProxyOrderKind[] AutoAssignKinds =
    [
        ProxyOrderKind.Static,
        ProxyOrderKind.Datacenter,
        ProxyOrderKind.RotateProxy
    ];

    public static ProxyOrderPageRequest BuildRequest(ProxyOrderKind kind, int page, int limit = DefaultPageLimit) =>
        new()
        {
            Page = page,
            Limit = limit,
            CategoryTypeId = kind is ProxyOrderKind.RotateProxy or ProxyOrderKind.RotateKey ? 2 : 1,
            IsCdk = kind == ProxyOrderKind.RotateProxy ? false : kind == ProxyOrderKind.RotateKey ? true : null,
            OrderKind = kind
        };

    public static bool ShouldUseForAutoAssign(ProxyOrderKind kind, ProxyOrder order, DateTimeOffset now)
    {
        if (order.ExpiredAt is not null && order.ExpiredAt.Value <= now)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(order.ProxyAddress))
        {
            return false;
        }

        return kind switch
        {
            ProxyOrderKind.Datacenter => order.IsDatacenter,
            ProxyOrderKind.Static => !order.IsDatacenter,
            ProxyOrderKind.RotateProxy => true,
            _ => false
        };
    }
}
