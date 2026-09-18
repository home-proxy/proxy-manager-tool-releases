using FluentAssertions;
using NetAgent.ProxyManager.Core.Models;
using NetAgent.ProxyManager.Core.Services;

namespace NetAgent.ProxyManager.Tests;

public sealed class RoundRobinProxyAssignmentServiceTests
{
    [Fact]
    public void Assign_ShouldLoopThroughProxiesForEnabledAutoRules()
    {
        var proxies = new[]
        {
            new ProxyServer(),
            new ProxyServer(),
            new ProxyServer()
        };
        var rules = new[]
        {
            new ApplicationRule { ExecutableName = "chrome.exe" },
            new ApplicationRule { ExecutableName = "telegram.exe" },
            new ApplicationRule { ExecutableName = "gamecl.exe" },
            new ApplicationRule { ExecutableName = "nox.exe" }
        };

        var service = new RoundRobinProxyAssignmentService();
        service.Assign(rules, proxies);

        rules.Select(rule => rule.AssignedProxyId).Should().Equal(
            proxies[0].Id,
            proxies[1].Id,
            proxies[2].Id,
            proxies[0].Id);
    }

    [Fact]
    public void Assign_ShouldSkipDisabledOrManualRules()
    {
        var proxies = new[] { new ProxyServer(), new ProxyServer() };
        var manualProxyId = Guid.NewGuid();
        var rules = new[]
        {
            new ApplicationRule { ExecutableName = "chrome.exe", IsEnabled = false },
            new ApplicationRule { ExecutableName = "telegram.exe", AutoAssignProxy = false, AssignedProxyId = manualProxyId },
            new ApplicationRule { ExecutableName = "gamecl.exe" }
        };

        var service = new RoundRobinProxyAssignmentService();
        service.Assign(rules, proxies);

        rules[0].AssignedProxyId.Should().BeNull();
        rules[1].AssignedProxyId.Should().Be(manualProxyId);
        rules[2].AssignedProxyId.Should().Be(proxies[0].Id);
    }
}
