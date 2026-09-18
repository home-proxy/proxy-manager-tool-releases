using NetAgent.ProxyManager.Core.Models;

namespace NetAgent.ProxyManager.Core.Interfaces;

public interface IDepositApiClient
{
    Task<DepositTransaction> CreateDepositTransactionAsync(long amount, CancellationToken cancellationToken);

    Task<DepositTransaction> GetDepositTransactionAsync(string id, CancellationToken cancellationToken);
}
