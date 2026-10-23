using NetAgent.ProxyManager.Core.Models;

namespace NetAgent.ProxyManager.Core.Services;

public static class RotateProxyAssignmentAddress
{
    public static string Resolve(ProxyOrderKind kind, string? orderAddress, string? currentRotateProxy)
    {
        if (kind == ProxyOrderKind.RotateProxy && !string.IsNullOrWhiteSpace(currentRotateProxy))
        {
            return currentRotateProxy.Trim();
        }

        return orderAddress?.Trim() ?? string.Empty;
    }
}
