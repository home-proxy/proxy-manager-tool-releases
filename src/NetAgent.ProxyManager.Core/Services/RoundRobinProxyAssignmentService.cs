using NetAgent.ProxyManager.Core.Models;

namespace NetAgent.ProxyManager.Core.Services;

public sealed class RoundRobinProxyAssignmentService
{
    public IReadOnlyList<ApplicationRule> Assign(
        IReadOnlyList<ApplicationRule> rules,
        IReadOnlyList<ProxyServer> proxies)
    {
        var availableProxies = proxies.ToList();
        if (availableProxies.Count == 0)
        {
            return rules;
        }

        var proxyIndex = 0;
        foreach (var rule in rules.Where(rule => rule.IsEnabled && rule.AutoAssignProxy))
        {
            rule.AssignedProxyId = availableProxies[proxyIndex % availableProxies.Count].Id;
            proxyIndex++;
        }

        return rules;
    }
}
