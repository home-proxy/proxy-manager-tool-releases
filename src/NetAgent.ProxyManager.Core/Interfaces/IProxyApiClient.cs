using NetAgent.ProxyManager.Core.Models;

namespace NetAgent.ProxyManager.Core.Interfaces;

public interface IProxyApiClient
{
    Task<IReadOnlyList<ProxyServer>> GetMyProxiesAsync(CancellationToken cancellationToken);
    Task<ProxySessionDto> CreateProxySessionAsync(string countryCode, CancellationToken cancellationToken);
}
