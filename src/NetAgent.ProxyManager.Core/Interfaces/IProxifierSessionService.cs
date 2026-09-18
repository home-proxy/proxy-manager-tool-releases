using NetAgent.ProxyManager.Core.Models;

namespace NetAgent.ProxyManager.Core.Interfaces;

public interface IProxifierSessionService
{
    Task<ProxifierSessionResult> StartAsync(
        AppSettings settings,
        IReadOnlyList<ProxyServer> proxies,
        IReadOnlyList<ApplicationRule> applications,
        CancellationToken cancellationToken);

    Task<ProxifierSessionResult> StopAsync(
        AppSettings settings,
        CancellationToken cancellationToken);
}
