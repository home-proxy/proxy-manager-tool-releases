namespace NetAgent.ProxyManager.Core.Models;

public sealed record ProfileAffectingChange(
    string Reason,
    IReadOnlySet<Guid> AffectedRuleIds,
    IReadOnlySet<Guid> AffectedProxyIds,
    bool RestartAllEligibleApplications)
{
    public static readonly string DefaultReason = "Cập nhật cấu hình proxy";

    public static ProfileAffectingChange Global(string reason) =>
        new(NormalizeReason(reason), new HashSet<Guid>(), new HashSet<Guid>(), true);

    public static ProfileAffectingChange ForRules(string reason, IEnumerable<Guid> affectedRuleIds) =>
        new(
            NormalizeReason(reason),
            affectedRuleIds.Where(id => id != Guid.Empty).ToHashSet(),
            new HashSet<Guid>(),
            false);

    public static ProfileAffectingChange ForProxies(string reason, IEnumerable<Guid> affectedProxyIds) =>
        new(
            NormalizeReason(reason),
            new HashSet<Guid>(),
            affectedProxyIds.Where(id => id != Guid.Empty).ToHashSet(),
            false);

    public static ProfileAffectingChange ForRulesAndProxies(
        string reason,
        IEnumerable<Guid> affectedRuleIds,
        IEnumerable<Guid> affectedProxyIds) =>
        new(
            NormalizeReason(reason),
            affectedRuleIds.Where(id => id != Guid.Empty).ToHashSet(),
            affectedProxyIds.Where(id => id != Guid.Empty).ToHashSet(),
            false);

    public ProfileAffectingChange Merge(ProfileAffectingChange other)
    {
        ArgumentNullException.ThrowIfNull(other);

        return new ProfileAffectingChange(
            MergeReasons(Reason, other.Reason),
            AffectedRuleIds.Concat(other.AffectedRuleIds).ToHashSet(),
            AffectedProxyIds.Concat(other.AffectedProxyIds).ToHashSet(),
            RestartAllEligibleApplications || other.RestartAllEligibleApplications);
    }

    private static string MergeReasons(string first, string second)
    {
        first = NormalizeReason(first);
        second = NormalizeReason(second);

        return string.Equals(first, second, StringComparison.Ordinal)
            ? first
            : DefaultReason;
    }

    private static string NormalizeReason(string reason) =>
        string.IsNullOrWhiteSpace(reason) ? DefaultReason : reason.Trim();
}
