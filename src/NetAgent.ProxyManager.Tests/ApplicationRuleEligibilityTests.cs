using FluentAssertions;
using NetAgent.ProxyManager.Core.Models;
using NetAgent.ProxyManager.Core.Services;

namespace NetAgent.ProxyManager.Tests;

public sealed class ApplicationRuleEligibilityTests
{
    [Fact]
    public void Evaluate_ShouldRejectRuleWithoutAssignedProxy()
    {
        var result = ApplicationRuleEligibility.Evaluate(
            new ApplicationRule { ExecutableName = "chrome.exe" },
            Array.Empty<ProxyServer>());

        result.CanUseProxy.Should().BeFalse();
        result.Message.Should().Contain("proxy");
    }

    [Fact]
    public void Evaluate_ShouldRejectRuleAssignedToMissingProxy()
    {
        var result = ApplicationRuleEligibility.Evaluate(
            new ApplicationRule
            {
                ExecutableName = "chrome.exe",
                AssignedProxyId = Guid.NewGuid()
            },
            Array.Empty<ProxyServer>());

        result.CanUseProxy.Should().BeFalse();
        result.Message.Should().Be(ApplicationRuleEligibility.MissingAssignedProxyMessage);
    }

    [Fact]
    public void Evaluate_ShouldRejectExecutableRuleTargetingKnownEmulatorLauncher()
    {
        var proxy = CreateProxy();
        var result = ApplicationRuleEligibility.Evaluate(
            new ApplicationRule
            {
                ExecutableName = @"D:\setup\LDPlayer\LDPlayer9\dnplayer.exe",
                AssignedProxyId = proxy.Id
            },
            new[] { proxy });

        result.CanUseProxy.Should().BeFalse();
        result.Message.Should().Be(ApplicationRuleEligibility.EmulatorLauncherExecutableMessage);
    }

    [Fact]
    public void Evaluate_ShouldRejectStoppedEmulator()
    {
        var proxy = CreateProxy();
        var result = ApplicationRuleEligibility.Evaluate(
            new ApplicationRule
            {
                TargetType = ApplicationTargetType.Emulator,
                ProcessId = null,
                AssignedProxyId = proxy.Id
            },
            new[] { proxy });

        result.CanUseProxy.Should().BeFalse();
        result.Message.Should().Contain("giả lập");
    }

    [Fact]
    public void Evaluate_ShouldAcceptAssignedExecutable()
    {
        var proxy = CreateProxy();
        var result = ApplicationRuleEligibility.Evaluate(
            new ApplicationRule
            {
                ExecutableName = "chrome.exe",
                AssignedProxyId = proxy.Id
            },
            new[] { proxy });

        result.CanUseProxy.Should().BeTrue();
    }

    [Fact]
    public void Evaluate_ShouldRejectAssignedExecutableWhenProxyEndpointIsInvalid()
    {
        var proxy = CreateProxy(proxy: "127.0.0.1:not-a-port");
        var result = ApplicationRuleEligibility.Evaluate(
            new ApplicationRule
            {
                ExecutableName = "chrome.exe",
                AssignedProxyId = proxy.Id
            },
            new[] { proxy });

        result.CanUseProxy.Should().BeFalse();
        result.Message.Should().Contain("endpoint");
    }

    [Fact]
    public void Evaluate_ShouldRejectAssignedExecutableWhenProxyIsExpired()
    {
        var now = DateTimeOffset.Parse("2026-07-03T00:00:00Z");
        var proxy = CreateProxy(expiredAt: now.AddSeconds(-1));
        var result = ApplicationRuleEligibility.Evaluate(
            new ApplicationRule
            {
                ExecutableName = "chrome.exe",
                AssignedProxyId = proxy.Id
            },
            new[] { proxy },
            now);

        result.CanUseProxy.Should().BeFalse();
        result.Message.Should().Be(ApplicationRuleEligibility.ExpiredAssignedProxyMessage);
    }

    [Fact]
    public void Evaluate_ShouldAcceptRunningAssignedEmulator()
    {
        var proxy = CreateProxy();
        var result = ApplicationRuleEligibility.Evaluate(
            new ApplicationRule
            {
                TargetType = ApplicationTargetType.Emulator,
                ProcessId = 1234,
                AssignedProxyId = proxy.Id
            },
            new[] { proxy });

        result.CanUseProxy.Should().BeTrue();
    }

    private static ProxyServer CreateProxy(
        string proxy = "127.0.0.1:8080",
        DateTimeOffset? expiredAt = null) =>
        new()
        {
            Proxy = proxy,
            ExpiredAt = expiredAt
        };
}
