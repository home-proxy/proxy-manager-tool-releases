using FluentAssertions;
using NetAgent.ProxyManager.Core.Models;
using NetAgent.ProxyManager.Core.Services;

namespace NetAgent.ProxyManager.Tests;

public sealed class ExpiredBackendProxyAssignmentServiceTests
{
    [Fact]
    public void FindExpiredAssignments_ShouldReturnAffectedRulesOnly()
    {
        var expiredRuleId = Guid.NewGuid();
        var activeRuleId = Guid.NewGuid();
        var localOnlyRuleId = Guid.NewGuid();
        var expiredProxyId = Guid.NewGuid();
        var activeProxyId = Guid.NewGuid();
        var localOnlyProxyId = Guid.NewGuid();
        var rules = new List<ApplicationRule>
        {
            new()
            {
                Id = expiredRuleId,
                ExecutableName = "expired.exe",
                AssignedProxyId = expiredProxyId,
                IsEnabled = true,
                AutoAssignProxy = true
            },
            new()
            {
                Id = activeRuleId,
                ExecutableName = "active.exe",
                AssignedProxyId = activeProxyId,
                IsEnabled = true,
                AutoAssignProxy = true
            },
            new()
            {
                Id = localOnlyRuleId,
                ExecutableName = "local.exe",
                AssignedProxyId = localOnlyProxyId,
                IsEnabled = true,
                AutoAssignProxy = true
            }
        };
        var proxies = new List<ProxyServer>
        {
            new() { Id = expiredProxyId, Proxy = "127.0.0.1:8080", BackendUserProxyId = 10 },
            new() { Id = activeProxyId, Proxy = "127.0.0.1:8081", BackendUserProxyId = 11 },
            new() { Id = localOnlyProxyId, Proxy = "127.0.0.1:8082" }
        };
        var orders = new List<ProxyOrder>
        {
            new() { UserProxyId = 10, ExpiredAt = DateTimeOffset.UtcNow.AddMinutes(-1) },
            new() { UserProxyId = 11, ExpiredAt = DateTimeOffset.UtcNow.AddDays(1) }
        };

        var result = new ExpiredBackendProxyAssignmentService()
            .FindExpiredAssignments(orders, proxies, rules, DateTimeOffset.UtcNow);

        result.HasChanges.Should().BeTrue();
        result.AffectedApplicationCount.Should().Be(1);
        result.AffectedApplicationRuleIds.Should().Equal(expiredRuleId);
        result.AffectedApplicationNames.Should().Equal("expired.exe");
        rules[0].AssignedProxyId.Should().Be(expiredProxyId);
        rules[0].IsEnabled.Should().BeTrue();
        rules[0].AutoAssignProxy.Should().BeTrue();
        rules[1].AssignedProxyId.Should().Be(activeProxyId);
        rules[2].AssignedProxyId.Should().Be(localOnlyProxyId);
    }
}
