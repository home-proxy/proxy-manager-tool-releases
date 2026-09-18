using FluentAssertions;
using NetAgent.ProxyManager.Core.Models;
using NetAgent.ProxyManager.Core.Services;

namespace NetAgent.ProxyManager.Tests;

public sealed class BackendProxyOrderSourceTests
{
    [Fact]
    public void AutoAssignKinds_ShouldMatchVisibleBackendProxyTabs()
    {
        BackendProxyOrderSource.AutoAssignKinds.Should().Equal(
            ProxyOrderKind.Static,
            ProxyOrderKind.Datacenter,
            ProxyOrderKind.RotateProxy);
    }

    [Fact]
    public void BuildRequest_ShouldUseBackendCategoryAndKind()
    {
        var staticRequest = BackendProxyOrderSource.BuildRequest(ProxyOrderKind.Static, page: 2);
        var datacenterRequest = BackendProxyOrderSource.BuildRequest(ProxyOrderKind.Datacenter, page: 3);
        var rotateRequest = BackendProxyOrderSource.BuildRequest(ProxyOrderKind.RotateProxy, page: 4);

        staticRequest.Should().BeEquivalentTo(new ProxyOrderPageRequest
        {
            Page = 2,
            Limit = BackendProxyOrderSource.DefaultPageLimit,
            CategoryTypeId = 1,
            IsCdk = null,
            OrderKind = ProxyOrderKind.Static
        });
        datacenterRequest.Should().BeEquivalentTo(new ProxyOrderPageRequest
        {
            Page = 3,
            Limit = BackendProxyOrderSource.DefaultPageLimit,
            CategoryTypeId = 1,
            IsCdk = null,
            OrderKind = ProxyOrderKind.Datacenter
        });
        rotateRequest.Should().BeEquivalentTo(new ProxyOrderPageRequest
        {
            Page = 4,
            Limit = BackendProxyOrderSource.DefaultPageLimit,
            CategoryTypeId = 2,
            IsCdk = false,
            OrderKind = ProxyOrderKind.RotateProxy
        });
    }

    [Fact]
    public void ShouldUseForAutoAssign_ShouldFilterVisibleBackendTabOrders()
    {
        var now = DateTimeOffset.UtcNow;
        var staticOrder = CreateOrder(provider: "VNPT");
        var datacenterOrder = CreateOrder(provider: "US", ip: "103.51.120.98");
        var rotateOrder = CreateOrder(provider: "VNPT", categoryTypeId: 2);
        var expiredOrder = CreateOrder(provider: "VNPT", expiredAt: now.AddSeconds(-1));
        var emptyEndpointOrder = CreateOrder(provider: "VNPT", ip: string.Empty);

        BackendProxyOrderSource.ShouldUseForAutoAssign(ProxyOrderKind.Static, staticOrder, now).Should().BeTrue();
        BackendProxyOrderSource.ShouldUseForAutoAssign(ProxyOrderKind.Static, datacenterOrder, now).Should().BeFalse();
        BackendProxyOrderSource.ShouldUseForAutoAssign(ProxyOrderKind.Datacenter, datacenterOrder, now).Should().BeTrue();
        BackendProxyOrderSource.ShouldUseForAutoAssign(ProxyOrderKind.Datacenter, staticOrder, now).Should().BeFalse();
        BackendProxyOrderSource.ShouldUseForAutoAssign(ProxyOrderKind.RotateProxy, rotateOrder, now).Should().BeTrue();
        BackendProxyOrderSource.ShouldUseForAutoAssign(ProxyOrderKind.Static, expiredOrder, now).Should().BeFalse();
        BackendProxyOrderSource.ShouldUseForAutoAssign(ProxyOrderKind.Static, emptyEndpointOrder, now).Should().BeFalse();
    }

    private static ProxyOrder CreateOrder(
        string provider,
        string ip = "127.0.0.1",
        int categoryTypeId = 1,
        DateTimeOffset? expiredAt = null) =>
        new()
        {
            UserProxyId = 123,
            Provider = provider,
            Ip = ip,
            Port = 8080,
            Username = "user",
            Password = "pass",
            Protocol = ProxyProtocol.Https,
            CategoryTypeId = categoryTypeId,
            ExpiredAt = expiredAt
        };
}
