namespace NetAgent.ProxyManager.Core.Interfaces;

using NetAgent.ProxyManager.Core.Models;

public interface ILocalSecretStore
{
    Task<AuthSession?> GetSessionAsync(CancellationToken cancellationToken);
    Task SaveSessionAsync(AuthSession session, CancellationToken cancellationToken);
    Task ClearSessionAsync(CancellationToken cancellationToken);
}
