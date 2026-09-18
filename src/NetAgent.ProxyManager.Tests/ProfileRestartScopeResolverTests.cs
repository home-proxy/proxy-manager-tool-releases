using FluentAssertions;
using NetAgent.ProxyManager.Core.Models;
using NetAgent.ProxyManager.Core.Services;

namespace NetAgent.ProxyManager.Tests;

public sealed class ProfileRestartScopeResolverTests
{
    [Fact]
    public void ResolveTargetRuleIds_ShouldReturnNullForGlobalChange()
    {
        var result = ProfileRestartScopeResolver.ResolveTargetRuleIds(
            ProfileAffectingChange.Global("Bắt đầu dùng proxy"),
            []);

        result.Should().BeNull();
    }

    [Fact]
    public void ResolveTargetRuleIds_ShouldReturnOnlyAffectedRuleIds()
    {
        var chrome = new ApplicationRule { Id = Guid.NewGuid(), ExecutableName = "chrome.exe" };
        var zalo = new ApplicationRule { Id = Guid.NewGuid(), ExecutableName = "zalo.exe" };

        var result = ProfileRestartScopeResolver.ResolveTargetRuleIds(
            ProfileAffectingChange.ForRules("Cập nhật rule ứng dụng", [chrome.Id]),
            [chrome, zalo]);

        result.Should().BeEquivalentTo([chrome.Id]);
    }

    [Fact]
    public void ResolveTargetRuleIds_ShouldReturnRulesUsingAffectedProxy()
    {
        var sharedProxyId = Guid.NewGuid();
        var otherProxyId = Guid.NewGuid();
        var chrome = new ApplicationRule { Id = Guid.NewGuid(), AssignedProxyId = sharedProxyId };
        var zalo = new ApplicationRule { Id = Guid.NewGuid(), AssignedProxyId = sharedProxyId };
        var edge = new ApplicationRule { Id = Guid.NewGuid(), AssignedProxyId = otherProxyId };

        var result = ProfileRestartScopeResolver.ResolveTargetRuleIds(
            ProfileAffectingChange.ForProxies("Cập nhật proxy", [sharedProxyId]),
            [chrome, zalo, edge]);

        result.Should().BeEquivalentTo([chrome.Id, zalo.Id]);
    }

    [Fact]
    public void ResolveTargetRuleIds_ShouldCombineAffectedRulesAndProxyUsers()
    {
        var proxyId = Guid.NewGuid();
        var chrome = new ApplicationRule { Id = Guid.NewGuid() };
        var zalo = new ApplicationRule { Id = Guid.NewGuid(), AssignedProxyId = proxyId };

        var result = ProfileRestartScopeResolver.ResolveTargetRuleIds(
            ProfileAffectingChange.ForRulesAndProxies("Cập nhật cấu hình proxy", [chrome.Id], [proxyId]),
            [chrome, zalo]);

        result.Should().BeEquivalentTo([chrome.Id, zalo.Id]);
    }
}
