using FluentAssertions;
using NetAgent.ProxyManager.Core.Models;
using NetAgent.ProxyManager.Core.Services;

namespace NetAgent.ProxyManager.Tests;

public sealed class SystemProxyEligibilityTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ResolveDefaultRoute_ShouldKeepConfiguredDefaultWhenDisabled()
    {
        var settings = new AppSettings { DefaultRouteDirect = true };

        SystemProxyEligibility.ResolveDefaultRoute(settings, [], Now)
            .Should().Be(((Guid?)null, true));
    }

    [Fact]
    public void ResolveDefaultRoute_ShouldUseUsableWholePcProxy()
    {
        var proxy = new ProxyServer { Proxy = "203.0.113.20:1080", ExpiredAt = Now.AddDays(1) };
        var settings = new AppSettings { SystemProxyId = proxy.Id };

        SystemProxyEligibility.ResolveDefaultRoute(settings, [proxy], Now)
            .Should().Be(((Guid?)proxy.Id, true));
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("expired")]
    [InlineData("invalid-endpoint")]
    public void ResolveDefaultRoute_ShouldBlockInsteadOfLeakingWhenWholePcProxyIsUnusable(string scenario)
    {
        var proxy = scenario switch
        {
            "expired" => new ProxyServer { Proxy = "203.0.113.20:1080", ExpiredAt = Now.AddMinutes(-1) },
            "invalid-endpoint" => new ProxyServer { Proxy = "not-an-endpoint" },
            _ => new ProxyServer { Proxy = "203.0.113.20:1080" }
        };
        var settings = new AppSettings { SystemProxyId = proxy.Id, DefaultRouteDirect = true };
        IReadOnlyList<ProxyServer> proxies = scenario == "missing" ? [] : [proxy];

        SystemProxyEligibility.Evaluate(settings.SystemProxyId, proxies, Now).State
            .Should().Be(SystemProxyState.Unusable);
        SystemProxyEligibility.ResolveDefaultRoute(settings, proxies, Now)
            .Should().Be(((Guid?)null, false));
    }
}
