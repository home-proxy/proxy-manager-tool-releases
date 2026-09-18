using NetAgent.ProxyManager.Core.Models;

namespace NetAgent.ProxyManager.Core.Services;

public sealed class ExpiredBackendProxyAssignmentService
{
    public ExpiredBackendProxyAssignmentResult FindExpiredAssignments(
        IReadOnlyCollection<ProxyOrder> refreshedOrders,
        IReadOnlyCollection<ProxyServer> localProxies,
        IReadOnlyCollection<ApplicationRule> rules,
        DateTimeOffset now)
    {
        var expiredUserProxyIds = refreshedOrders
            .Where(order => order.ExpiredAt is not null && order.ExpiredAt.Value <= now)
            .Select(order => order.UserProxyId)
            .ToHashSet();
        if (expiredUserProxyIds.Count == 0)
        {
            return new ExpiredBackendProxyAssignmentResult([], []);
        }

        var expiredLocalProxyIds = localProxies
            .Where(proxy => proxy.BackendUserProxyId is not null && expiredUserProxyIds.Contains(proxy.BackendUserProxyId.Value))
            .Select(proxy => proxy.Id)
            .ToHashSet();
        if (expiredLocalProxyIds.Count == 0)
        {
            return new ExpiredBackendProxyAssignmentResult([], []);
        }

        var affectedRuleIds = new List<Guid>();
        var affectedNames = new List<string>();
        foreach (var rule in rules.Where(rule => rule.AssignedProxyId is not null && expiredLocalProxyIds.Contains(rule.AssignedProxyId.Value)))
        {
            affectedRuleIds.Add(rule.Id);
            affectedNames.Add(rule.GetApplicationName());
        }

        return new ExpiredBackendProxyAssignmentResult(affectedRuleIds, affectedNames);
    }
}
