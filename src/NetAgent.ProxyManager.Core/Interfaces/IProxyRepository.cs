using NetAgent.ProxyManager.Core.Models;

namespace NetAgent.ProxyManager.Core.Interfaces;

public interface IProxyRepository
{
    Task<IReadOnlyList<ProxyServer>> GetAllAsync(CancellationToken cancellationToken);
    Task SaveAllAsync(IReadOnlyCollection<ProxyServer> proxies, CancellationToken cancellationToken);
}
