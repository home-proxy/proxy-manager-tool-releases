namespace NetAgent.ProxyManager.Core.Models;

public sealed record ExpiredBackendProxyAssignmentResult(
    IReadOnlyList<Guid> AffectedApplicationRuleIds,
    IReadOnlyList<string> AffectedApplicationNames)
{
    public int AffectedApplicationCount => AffectedApplicationRuleIds.Count;
    public bool HasChanges => AffectedApplicationCount > 0;
}
