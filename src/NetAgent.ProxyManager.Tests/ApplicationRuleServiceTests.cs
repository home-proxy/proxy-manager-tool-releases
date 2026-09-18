using FluentAssertions;
using NetAgent.ProxyManager.Core.Interfaces;
using NetAgent.ProxyManager.Core.Models;
using NetAgent.ProxyManager.Core.Services;

namespace NetAgent.ProxyManager.Tests;

public sealed class ApplicationRuleServiceTests
{
    [Fact]
    public void ClearMissingProxyAssignments_ShouldKeepRulesAndClearOnlyOrphanedAssignments()
    {
        var existingProxy = new ProxyServer { Proxy = "127.0.0.1:8080" };
        var missingProxyId = Guid.NewGuid();
        var orphanedRule = new ApplicationRule
        {
            ExecutableName = "chrome.exe",
            AssignedProxyId = missingProxyId,
            IsEnabled = true
        };
        var validRule = new ApplicationRule
        {
            ExecutableName = "edge.exe",
            AssignedProxyId = existingProxy.Id,
            IsEnabled = true
        };
        var service = CreateService();

        var clearedCount = service.ClearMissingProxyAssignments([orphanedRule, validRule], [existingProxy]);

        clearedCount.Should().Be(1);
        orphanedRule.AssignedProxyId.Should().BeNull();
        orphanedRule.IsEnabled.Should().BeFalse();
        orphanedRule.Warning.Should().Be(ApplicationRuleEligibility.MissingAssignedProxyMessage);
        validRule.AssignedProxyId.Should().Be(existingProxy.Id);
        validRule.IsEnabled.Should().BeTrue();
    }

    [Fact]
    public void ClearExpiredProxyAssignments_ShouldKeepRulesAndClearExpiredLocalProxyAssignments()
    {
        var expiredProxy = new ProxyServer
        {
            Proxy = "127.0.0.1:8080",
            ExpiredAt = DateTimeOffset.UtcNow.AddMinutes(-1)
        };
        var activeProxy = new ProxyServer
        {
            Proxy = "127.0.0.1:8081",
            ExpiredAt = DateTimeOffset.UtcNow.AddMinutes(10)
        };
        var expiredRule = new ApplicationRule
        {
            ExecutableName = "expired.exe",
            AssignedProxyId = expiredProxy.Id,
            IsEnabled = true
        };
        var activeRule = new ApplicationRule
        {
            ExecutableName = "active.exe",
            AssignedProxyId = activeProxy.Id,
            IsEnabled = true
        };
        var service = CreateService();

        var clearedCount = service.ClearExpiredProxyAssignments(
            [expiredRule, activeRule],
            [expiredProxy, activeProxy],
            DateTimeOffset.UtcNow);

        clearedCount.Should().Be(1);
        expiredRule.AssignedProxyId.Should().BeNull();
        expiredRule.IsEnabled.Should().BeFalse();
        expiredRule.Warning.Should().Be(ApplicationRuleEligibility.MissingAssignedProxyMessage);
        activeRule.AssignedProxyId.Should().Be(activeProxy.Id);
        activeRule.IsEnabled.Should().BeTrue();
    }

    [Fact]
    public void ClearProxyAssignmentsByRuleId_ShouldKeepExpiredRulesAndClearTheirAssignments()
    {
        var expiredProxyId = Guid.NewGuid();
        var activeProxyId = Guid.NewGuid();
        var expiredRule = new ApplicationRule
        {
            ExecutableName = "expired.exe",
            AssignedProxyId = expiredProxyId,
            IsEnabled = true
        };
        var activeRule = new ApplicationRule
        {
            ExecutableName = "active.exe",
            AssignedProxyId = activeProxyId,
            IsEnabled = true
        };
        var rules = new[] { expiredRule, activeRule };
        var service = CreateService();

        var clearedCount = service.ClearProxyAssignmentsByRuleId(rules, [expiredRule.Id]);

        clearedCount.Should().Be(1);
        rules.Should().HaveCount(2);
        expiredRule.AssignedProxyId.Should().BeNull();
        expiredRule.IsEnabled.Should().BeFalse();
        expiredRule.Warning.Should().Be(ApplicationRuleEligibility.MissingAssignedProxyMessage);
        activeRule.AssignedProxyId.Should().Be(activeProxyId);
        activeRule.IsEnabled.Should().BeTrue();
    }

    [Fact]
    public void ClearProxyAssignmentsByProxyId_ShouldClearRulesAssignedToMissingBackendProxy()
    {
        var missingBackendProxyId = Guid.NewGuid();
        var activeProxyId = Guid.NewGuid();
        var staleRule = new ApplicationRule
        {
            ExecutableName = "stale.exe",
            AssignedProxyId = missingBackendProxyId,
            IsEnabled = true
        };
        var activeRule = new ApplicationRule
        {
            ExecutableName = "active.exe",
            AssignedProxyId = activeProxyId,
            IsEnabled = true
        };
        var service = CreateService();

        var clearedCount = service.ClearProxyAssignmentsByProxyId([staleRule, activeRule], [missingBackendProxyId]);

        clearedCount.Should().Be(1);
        staleRule.AssignedProxyId.Should().BeNull();
        staleRule.IsEnabled.Should().BeFalse();
        staleRule.Warning.Should().Be(ApplicationRuleEligibility.MissingAssignedProxyMessage);
        activeRule.AssignedProxyId.Should().Be(activeProxyId);
        activeRule.IsEnabled.Should().BeTrue();
    }

    [Fact]
    public void AutoAssignRoundRobin_ShouldForceEnabledRulesWhenRequested()
    {
        var proxies = new[]
        {
            new ProxyServer { Proxy = "127.0.0.1:8001" },
            new ProxyServer { Proxy = "127.0.0.1:8002" }
        };
        var rules = new[]
        {
            new ApplicationRule { ExecutableName = "bs1.exe", AutoAssignProxy = false, IsEnabled = true },
            new ApplicationRule { ExecutableName = "bs2.exe", AutoAssignProxy = false, IsEnabled = true },
            new ApplicationRule { ExecutableName = "disabled.exe", AutoAssignProxy = false, IsEnabled = false }
        };
        var service = new ApplicationRuleService(
            new InMemoryApplicationRuleRepository(),
            new RoundRobinProxyAssignmentService(),
            new StubEmulatorProcessScanner([]));

        service.AutoAssignRoundRobin(rules, proxies, forceAllEnabledRules: true);

        rules[0].AssignedProxyId.Should().Be(proxies[0].Id);
        rules[0].AutoAssignProxy.Should().BeTrue();
        rules[1].AssignedProxyId.Should().Be(proxies[1].Id);
        rules[1].AutoAssignProxy.Should().BeTrue();
        rules[2].AssignedProxyId.Should().BeNull();
        rules[2].AutoAssignProxy.Should().BeFalse();
    }

    [Fact]
    public void AutoAssignRoundRobinToTargets_ShouldAssignDisabledRulesAndEnableThem()
    {
        var proxies = new[]
        {
            new ProxyServer { Proxy = "127.0.0.1:8001" },
            new ProxyServer { Proxy = "127.0.0.1:8002" }
        };
        var rules = new[]
        {
            new ApplicationRule { ExecutableName = "chrome.exe", AutoAssignProxy = false, IsEnabled = false },
            new ApplicationRule { ExecutableName = "zalo.exe", AutoAssignProxy = false, IsEnabled = false },
            new ApplicationRule { ExecutableName = "edge.exe", AutoAssignProxy = false, IsEnabled = false }
        };
        var service = CreateService();

        service.AutoAssignRoundRobinToTargets(rules, proxies, enableAssignedRules: true);

        rules.Select(rule => rule.AssignedProxyId).Should().Equal(
            proxies[0].Id,
            proxies[1].Id,
            proxies[0].Id);
        rules.Should().OnlyContain(rule => rule.IsEnabled);
        rules.Should().OnlyContain(rule => rule.AutoAssignProxy);
        rules.Should().OnlyContain(rule => rule.Warning == null);
    }

    [Fact]
    public void AutoAssignRoundRobinToTargets_ShouldSkipRulesWithoutUsableTarget()
    {
        var proxy = new ProxyServer { Proxy = "127.0.0.1:8001" };
        var validExecutable = new ApplicationRule { ExecutableName = "chrome.exe", IsEnabled = false };
        var missingExecutable = new ApplicationRule { ExecutableName = "", IsEnabled = false };
        var stoppedEmulator = new ApplicationRule
        {
            TargetType = ApplicationTargetType.Emulator,
            ProcessId = null,
            IsEnabled = false
        };
        var runningEmulator = new ApplicationRule
        {
            TargetType = ApplicationTargetType.Emulator,
            ProcessId = 1234,
            IsEnabled = false
        };
        var service = CreateService();

        service.AutoAssignRoundRobinToTargets(
            [validExecutable, missingExecutable, stoppedEmulator, runningEmulator],
            [proxy],
            enableAssignedRules: true);

        validExecutable.AssignedProxyId.Should().Be(proxy.Id);
        validExecutable.IsEnabled.Should().BeTrue();
        runningEmulator.AssignedProxyId.Should().Be(proxy.Id);
        runningEmulator.IsEnabled.Should().BeTrue();
        missingExecutable.AssignedProxyId.Should().BeNull();
        missingExecutable.IsEnabled.Should().BeFalse();
        stoppedEmulator.AssignedProxyId.Should().BeNull();
        stoppedEmulator.IsEnabled.Should().BeFalse();
    }

    [Fact]
    public void AutoAssignRoundRobinToTargets_ShouldIgnoreInvalidProxyEndpoints()
    {
        var proxy = new ProxyServer { Proxy = "127.0.0.1:8001" };
        var rule = new ApplicationRule { ExecutableName = "chrome.exe", IsEnabled = false };
        var service = CreateService();

        service.AutoAssignRoundRobinToTargets(
            [rule],
            [new ProxyServer { Proxy = "invalid" }, proxy],
            enableAssignedRules: true);

        rule.AssignedProxyId.Should().Be(proxy.Id);
        rule.IsEnabled.Should().BeTrue();
    }

    [Fact]
    public async Task RefreshEmulatorRuntimeTargetsAsync_ShouldNotMapStoppedRuleToTheOnlyRunningSameKindInstance()
    {
        var stoppedRule = new ApplicationRule
        {
            TargetType = ApplicationTargetType.Emulator,
            EmulatorKind = EmulatorKind.Nox,
            EmulatorInstanceKey = "Nox_0",
            EmulatorInstanceName = "Nox-00",
            RuntimeProcessName = "NoxVMHandle.exe",
            ProcessId = 101
        };
        var runningRule = new ApplicationRule
        {
            TargetType = ApplicationTargetType.Emulator,
            EmulatorKind = EmulatorKind.Nox,
            EmulatorInstanceKey = "Nox_1",
            EmulatorInstanceName = "Nox-01",
            RuntimeProcessName = "NoxVMHandle.exe",
            ProcessId = 102
        };
        var candidate = new EmulatorProcessCandidate(
            ProcessId: 20336,
            ParentProcessId: null,
            EmulatorKind: EmulatorKind.Nox,
            EmulatorInstanceKey: "Nox_1",
            EmulatorInstanceName: "Nox-01",
            RuntimeType: "headless",
            ProcessName: "NoxVMHandle.exe",
            ExecutablePath: null,
            CommandLine: "--comment Nox_1",
            MainWindowTitle: null);
        var service = new ApplicationRuleService(
            new InMemoryApplicationRuleRepository(),
            new RoundRobinProxyAssignmentService(),
            new StubEmulatorProcessScanner([candidate]));

        await service.RefreshEmulatorRuntimeTargetsAsync([stoppedRule, runningRule], CancellationToken.None);

        stoppedRule.ProcessId.Should().BeNull();
        stoppedRule.EmulatorInstanceKey.Should().Be("Nox_0");
        stoppedRule.EmulatorInstanceName.Should().Be("Nox-00");
        runningRule.ProcessId.Should().Be(20336);
        runningRule.EmulatorInstanceKey.Should().Be("Nox_1");
        runningRule.EmulatorInstanceName.Should().Be("Nox-01");
    }

    private static ApplicationRuleService CreateService() =>
        new(
            new InMemoryApplicationRuleRepository(),
            new RoundRobinProxyAssignmentService(),
            new StubEmulatorProcessScanner([]));

    private sealed class StubEmulatorProcessScanner(IReadOnlyList<EmulatorProcessCandidate> candidates) : IEmulatorProcessScanner
    {
        public Task<IReadOnlyList<EmulatorProcessCandidate>> ScanAsync(
            IReadOnlyCollection<EmulatorKind> emulatorKinds,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(candidates);
        }
    }

    private sealed class InMemoryApplicationRuleRepository : IApplicationRuleRepository
    {
        public Task<IReadOnlyList<ApplicationRule>> GetAllAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<ApplicationRule>>([]);

        public Task SaveAllAsync(IReadOnlyCollection<ApplicationRule> rules, CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }
}
