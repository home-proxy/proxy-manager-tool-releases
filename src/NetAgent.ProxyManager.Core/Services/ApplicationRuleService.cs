using NetAgent.ProxyManager.Core.Interfaces;
using NetAgent.ProxyManager.Core.Models;

namespace NetAgent.ProxyManager.Core.Services;

public sealed class ApplicationRuleService(
    IApplicationRuleRepository repository,
    RoundRobinProxyAssignmentService roundRobinService,
    IEmulatorProcessScanner emulatorProcessScanner)
{
    public Task<IReadOnlyList<ApplicationRule>> GetAllAsync(CancellationToken cancellationToken) =>
        repository.GetAllAsync(cancellationToken);

    public Task SaveAllAsync(IReadOnlyCollection<ApplicationRule> rules, CancellationToken cancellationToken) =>
        repository.SaveAllAsync(rules, cancellationToken);

    public int ClearMissingProxyAssignments(
        IReadOnlyList<ApplicationRule> rules,
        IReadOnlyCollection<ProxyServer> proxies)
    {
        var proxyIds = proxies.Select(proxy => proxy.Id).ToHashSet();
        return ClearProxyAssignments(
            rules,
            rule => rule.AssignedProxyId is { } proxyId && !proxyIds.Contains(proxyId));
    }

    public int ClearExpiredProxyAssignments(
        IReadOnlyList<ApplicationRule> rules,
        IReadOnlyCollection<ProxyServer> proxies,
        DateTimeOffset now)
    {
        var expiredProxyIds = proxies
            .Where(proxy => proxy.ExpiredAt is not null && proxy.ExpiredAt.Value <= now)
            .Select(proxy => proxy.Id)
            .ToHashSet();

        return ClearProxyAssignmentsByProxyId(rules, expiredProxyIds);
    }

    public int ClearProxyAssignmentsByRuleId(
        IReadOnlyList<ApplicationRule> rules,
        IReadOnlyCollection<Guid> ruleIds)
    {
        var targetRuleIds = ruleIds.ToHashSet();
        return ClearProxyAssignments(rules, rule => targetRuleIds.Contains(rule.Id));
    }

    public int ClearProxyAssignmentsByProxyId(
        IReadOnlyList<ApplicationRule> rules,
        IReadOnlyCollection<Guid> proxyIds)
    {
        var targetProxyIds = proxyIds.ToHashSet();
        return ClearProxyAssignments(
            rules,
            rule => rule.AssignedProxyId is { } proxyId && targetProxyIds.Contains(proxyId));
    }

    public IReadOnlyList<ApplicationRule> AutoAssignRoundRobin(
        IReadOnlyList<ApplicationRule> rules,
        IReadOnlyList<ProxyServer> proxies,
        bool forceAllEnabledRules = false)
    {
        if (forceAllEnabledRules)
        {
            foreach (var rule in rules.Where(rule => rule.IsEnabled))
            {
                rule.AutoAssignProxy = true;
            }
        }

        return roundRobinService.Assign(rules, proxies);
    }

    public IReadOnlyList<ApplicationRule> AutoAssignRoundRobinToTargets(
        IReadOnlyList<ApplicationRule> targetRules,
        IReadOnlyList<ProxyServer> proxies,
        bool enableAssignedRules)
    {
        var availableProxies = proxies
            .Where(proxy => proxy.HasValidEndpoint)
            .ToList();
        if (availableProxies.Count == 0)
        {
            return targetRules;
        }

        var proxyIndex = 0;
        foreach (var rule in targetRules.Where(CanReceiveAutoAssignedProxy))
        {
            rule.AssignedProxyId = availableProxies[proxyIndex % availableProxies.Count].Id;
            rule.AutoAssignProxy = true;
            rule.Warning = null;
            if (enableAssignedRules)
            {
                rule.IsEnabled = true;
            }

            proxyIndex++;
        }

        return targetRules;
    }

    public static bool CanReceiveAutoAssignedProxy(ApplicationRule rule)
    {
        if (rule.TargetType == ApplicationTargetType.Executable)
        {
            return !string.IsNullOrWhiteSpace(rule.ExecutableName);
        }

        return rule.TargetType == ApplicationTargetType.Emulator &&
            rule.ProcessId is > 0;
    }

    private static int ClearProxyAssignments(
        IReadOnlyList<ApplicationRule> rules,
        Func<ApplicationRule, bool> shouldClear)
    {
        var clearedCount = 0;
        foreach (var rule in rules.Where(rule => rule.AssignedProxyId is not null && shouldClear(rule)))
        {
            rule.AssignedProxyId = null;
            rule.IsEnabled = false;
            rule.Warning = ApplicationRuleEligibility.MissingAssignedProxyMessage;
            clearedCount++;
        }

        return clearedCount;
    }

    public IReadOnlyDictionary<string, int> GetDuplicateExecutableCounts(IEnumerable<ApplicationRule> rules)
    {
        return rules
            .Where(rule => rule.TargetType == ApplicationTargetType.Executable && !string.IsNullOrWhiteSpace(rule.ExecutableName))
            .GroupBy(rule => ApplicationRule.GetExecutableDisplayName(rule.ExecutableName), StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.OrdinalIgnoreCase);
    }

    public async Task<IReadOnlyList<ApplicationRule>> RefreshEmulatorRuntimeTargetsAsync(
        IReadOnlyList<ApplicationRule> rules,
        CancellationToken cancellationToken)
    {
        var emulatorRules = rules
            .Where(rule => rule.TargetType == ApplicationTargetType.Emulator && rule.EmulatorKind is not null)
            .ToList();
        if (emulatorRules.Count == 0)
        {
            return rules;
        }

        var kinds = emulatorRules
            .Select(rule => rule.EmulatorKind!.Value)
            .Distinct()
            .ToList();
        var candidates = await emulatorProcessScanner.ScanAsync(kinds, cancellationToken);

        var assignedProcessIds = new HashSet<int>();
        foreach (var rule in emulatorRules)
        {
            var candidate = FindCandidate(
                rule,
                candidates.Where(candidate => !assignedProcessIds.Contains(candidate.ProcessId)).ToList());
            if (candidate is null)
            {
                rule.ProcessId = null;
                continue;
            }

            assignedProcessIds.Add(candidate.ProcessId);
            rule.ProcessId = candidate.ProcessId;
            rule.EmulatorInstanceKey = candidate.EmulatorInstanceKey;
            rule.EmulatorInstanceName = candidate.EmulatorInstanceName;
            rule.RuntimeProcessName = candidate.ProcessName;
            rule.RuntimeExecutablePath = candidate.ExecutablePath;
        }

        return rules;
    }

    private static EmulatorProcessCandidate? FindCandidate(
        ApplicationRule rule,
        IReadOnlyList<EmulatorProcessCandidate> candidates)
    {
        var sameKind = candidates
            .Where(candidate => candidate.EmulatorKind == rule.EmulatorKind)
            .ToList();

        var byInstanceKey = sameKind.FirstOrDefault(candidate =>
            !string.IsNullOrWhiteSpace(rule.EmulatorInstanceKey) &&
            string.Equals(candidate.EmulatorInstanceKey, rule.EmulatorInstanceKey, StringComparison.OrdinalIgnoreCase) &&
            ProcessNameMatches(rule, candidate));
        if (byInstanceKey is not null)
        {
            return byInstanceKey;
        }

        var byLegacyKeyInCommandLine = sameKind.FirstOrDefault(candidate =>
            !string.IsNullOrWhiteSpace(rule.EmulatorInstanceKey) &&
            ProcessNameMatches(rule, candidate) &&
            CandidateCommandLineContainsInstanceKey(candidate, rule.EmulatorInstanceKey));
        if (byLegacyKeyInCommandLine is not null)
        {
            return byLegacyKeyInCommandLine;
        }

        var byInstanceName = sameKind.FirstOrDefault(candidate =>
            !string.IsNullOrWhiteSpace(rule.EmulatorInstanceName) &&
            string.Equals(candidate.EmulatorInstanceName, rule.EmulatorInstanceName, StringComparison.OrdinalIgnoreCase) &&
            ProcessNameMatches(rule, candidate));
        if (byInstanceName is not null)
        {
            return byInstanceName;
        }

        return HasKnownInstanceIdentity(rule) ? null : sameKind.Count == 1 ? sameKind[0] : null;
    }

    private static bool HasKnownInstanceIdentity(ApplicationRule rule) =>
        !string.IsNullOrWhiteSpace(rule.EmulatorInstanceKey) ||
        !string.IsNullOrWhiteSpace(rule.EmulatorInstanceName);

    private static bool CandidateCommandLineContainsInstanceKey(EmulatorProcessCandidate candidate, string instanceKey)
    {
        var commandLine = candidate.CommandLine ?? string.Empty;
        var normalizedKey = instanceKey.Trim().Trim('"', '{', '}');
        return !string.IsNullOrWhiteSpace(normalizedKey) &&
            (commandLine.Contains(normalizedKey, StringComparison.OrdinalIgnoreCase) ||
                commandLine.Contains($"{{{normalizedKey}}}", StringComparison.OrdinalIgnoreCase));
    }

    private static bool ProcessNameMatches(ApplicationRule rule, EmulatorProcessCandidate candidate)
    {
        return string.IsNullOrWhiteSpace(rule.RuntimeProcessName) ||
            string.Equals(rule.RuntimeProcessName, candidate.ProcessName, StringComparison.OrdinalIgnoreCase);
    }
}
