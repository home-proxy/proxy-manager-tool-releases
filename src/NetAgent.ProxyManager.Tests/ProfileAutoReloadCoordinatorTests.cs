using FluentAssertions;
using NetAgent.ProxyManager.Core.Models;
using NetAgent.ProxyManager.Core.Services;

namespace NetAgent.ProxyManager.Tests;

public sealed class ProfileAutoReloadCoordinatorTests
{
    [Fact]
    public async Task RequestReloadAsync_ShouldDoNothingWhenGateIsClosed()
    {
        var calls = 0;
        var coordinator = new ProfileAutoReloadCoordinator(
            shouldReload: () => false,
            reloadAsync: (_, _) =>
            {
                calls++;
                return Task.CompletedTask;
            });

        await coordinator.RequestReloadAsync(ProfileAffectingChange.Global("stopped"), CancellationToken.None);

        calls.Should().Be(0);
    }

    [Fact]
    public async Task RequestReloadAsync_ShouldReloadWhenGateIsOpen()
    {
        var changes = new List<ProfileAffectingChange>();
        var coordinator = new ProfileAutoReloadCoordinator(
            shouldReload: () => true,
            reloadAsync: (change, _) =>
            {
                changes.Add(change);
                return Task.CompletedTask;
            });

        await coordinator.RequestReloadAsync(ProfileAffectingChange.Global("running"), CancellationToken.None);

        changes.Select(change => change.Reason).Should().Equal("running");
        changes[0].RestartAllEligibleApplications.Should().BeTrue();
    }

    [Fact]
    public async Task RequestReloadAsync_ShouldCoalescePendingReloads()
    {
        var releaseFirstReload = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var firstReloadStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var firstRuleId = Guid.NewGuid();
        var secondRuleId = Guid.NewGuid();
        var proxyId = Guid.NewGuid();
        var changes = new List<ProfileAffectingChange>();
        var coordinator = new ProfileAutoReloadCoordinator(
            shouldReload: () => true,
            reloadAsync: async (change, _) =>
            {
                changes.Add(change);
                if (changes.Count == 1)
                {
                    firstReloadStarted.SetResult();
                    await releaseFirstReload.Task;
                }
            });

        var firstRequest = coordinator.RequestReloadAsync(
            ProfileAffectingChange.ForRules("first", [firstRuleId]),
            CancellationToken.None);
        await firstReloadStarted.Task;

        await coordinator.RequestReloadAsync(
            ProfileAffectingChange.ForRules("second", [secondRuleId]),
            CancellationToken.None);
        await coordinator.RequestReloadAsync(
            ProfileAffectingChange.ForProxies("third", [proxyId]),
            CancellationToken.None);
        releaseFirstReload.SetResult();
        await firstRequest;

        changes.Select(change => change.Reason).Should().Equal("first", ProfileAffectingChange.DefaultReason);
        changes[1].RestartAllEligibleApplications.Should().BeFalse();
        changes[1].AffectedRuleIds.Should().BeEquivalentTo([secondRuleId]);
        changes[1].AffectedProxyIds.Should().BeEquivalentTo([proxyId]);
    }

    [Fact]
    public async Task RequestReloadAsync_ShouldLetGlobalPendingReloadWinOverTargetedReloads()
    {
        var releaseFirstReload = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var firstReloadStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var changes = new List<ProfileAffectingChange>();
        var coordinator = new ProfileAutoReloadCoordinator(
            shouldReload: () => true,
            reloadAsync: async (change, _) =>
            {
                changes.Add(change);
                if (changes.Count == 1)
                {
                    firstReloadStarted.SetResult();
                    await releaseFirstReload.Task;
                }
            });

        var firstRequest = coordinator.RequestReloadAsync(
            ProfileAffectingChange.ForRules("first", [Guid.NewGuid()]),
            CancellationToken.None);
        await firstReloadStarted.Task;

        await coordinator.RequestReloadAsync(ProfileAffectingChange.Global("global"), CancellationToken.None);
        await coordinator.RequestReloadAsync(
            ProfileAffectingChange.ForProxies("proxy", [Guid.NewGuid()]),
            CancellationToken.None);
        releaseFirstReload.SetResult();
        await firstRequest;

        changes.Should().HaveCount(2);
        changes[1].RestartAllEligibleApplications.Should().BeTrue();
        changes[1].Reason.Should().Be(ProfileAffectingChange.DefaultReason);
    }
}
