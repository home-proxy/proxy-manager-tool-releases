using NetAgent.ProxyManager.Core.Models;

namespace NetAgent.ProxyManager.Core.Services;

public static class ProfileRestartScopeResolver
{
    public static IReadOnlySet<Guid>? ResolveTargetRuleIds(
        ProfileAffectingChange? change,
        IReadOnlyCollection<ApplicationRule> applications)
    {
        if (change is null || change.RestartAllEligibleApplications)
        {
            return null;
        }

        var affectedRuleIds = change.AffectedRuleIds.ToHashSet();
        if (change.AffectedProxyIds.Count > 0)
        {
            affectedRuleIds.UnionWith(applications
                .Where(rule => rule.AssignedProxyId is not null &&
                    change.AffectedProxyIds.Contains(rule.AssignedProxyId.Value))
                .Select(rule => rule.Id));
        }

        return affectedRuleIds;
    }
}
