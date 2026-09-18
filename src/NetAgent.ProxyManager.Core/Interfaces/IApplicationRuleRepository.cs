using NetAgent.ProxyManager.Core.Models;

namespace NetAgent.ProxyManager.Core.Interfaces;

public interface IApplicationRuleRepository
{
    Task<IReadOnlyList<ApplicationRule>> GetAllAsync(CancellationToken cancellationToken);
    Task SaveAllAsync(IReadOnlyCollection<ApplicationRule> rules, CancellationToken cancellationToken);
}
