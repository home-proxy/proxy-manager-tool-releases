using NetAgent.ProxyManager.Core.Models;

namespace NetAgent.ProxyManager.Core.Interfaces;

public interface IProxyChecker
{
    Task<ProxyCheckResult> CheckAsync(ProxyServer proxy, CancellationToken cancellationToken);
}
