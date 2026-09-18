using FluentAssertions;
using NetAgent.ProxyManager.Core.Services;

namespace NetAgent.ProxyManager.Tests;

public sealed class ManualProxyRotateGuardTests
{
    [Fact]
    public void TryBegin_ShouldBlockWhileRotateIsInFlight()
    {
        var now = DateTimeOffset.UtcNow;
        var guard = new ManualProxyRotateGuard(TimeSpan.FromSeconds(60), () => now);

        guard.TryBegin(out _).Should().BeTrue();

        guard.TryBegin(out var remaining).Should().BeFalse();
        remaining.Should().Be(TimeSpan.FromSeconds(60));
    }

    [Fact]
    public void FinishSucceeded_ShouldStartCooldown()
    {
        var now = DateTimeOffset.UtcNow;
        var guard = new ManualProxyRotateGuard(TimeSpan.FromSeconds(60), () => now);

        guard.TryBegin(out _).Should().BeTrue();
        guard.Finish(succeeded: true);
        now = now.AddSeconds(1);

        guard.TryBegin(out var remaining).Should().BeFalse();
        remaining.TotalSeconds.Should().BeApproximately(59, 0.01);
    }

    [Fact]
    public void FinishFailed_ShouldReleaseWithoutCooldown()
    {
        var now = DateTimeOffset.UtcNow;
        var guard = new ManualProxyRotateGuard(TimeSpan.FromSeconds(60), () => now);

        guard.TryBegin(out _).Should().BeTrue();
        guard.Finish(succeeded: false);

        guard.TryBegin(out _).Should().BeTrue();
    }

    [Fact]
    public void TryBegin_ShouldAllowAfterCooldownExpires()
    {
        var now = DateTimeOffset.UtcNow;
        var guard = new ManualProxyRotateGuard(TimeSpan.FromSeconds(60), () => now);

        guard.TryBegin(out _).Should().BeTrue();
        guard.Finish(succeeded: true);
        now = now.AddSeconds(60);

        guard.TryBegin(out _).Should().BeTrue();
    }
}
