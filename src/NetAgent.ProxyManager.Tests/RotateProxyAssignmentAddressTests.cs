using FluentAssertions;
using NetAgent.ProxyManager.Core.Models;
using NetAgent.ProxyManager.Core.Services;

namespace NetAgent.ProxyManager.Tests;

public sealed class RotateProxyAssignmentAddressTests
{
    [Fact]
    public void Resolve_ShouldUseCurrentRotateProxyInsteadOfOrderAddress()
    {
        var address = RotateProxyAssignmentAddress.Resolve(
            ProxyOrderKind.RotateProxy,
            "180.93.2.171:3129:user:old",
            " 14.187.1.2:3129:user:live ");

        address.Should().Be("14.187.1.2:3129:user:live");
    }

    [Fact]
    public void Resolve_ShouldKeepOrderAddressWhenCurrentRotateProxyIsMissing()
    {
        var address = RotateProxyAssignmentAddress.Resolve(
            ProxyOrderKind.RotateProxy,
            "180.93.2.171:3129:user:old",
            "  ");

        address.Should().Be("180.93.2.171:3129:user:old");
    }

    [Theory]
    [InlineData(ProxyOrderKind.Static)]
    [InlineData(ProxyOrderKind.Datacenter)]
    [InlineData(ProxyOrderKind.RotateKey)]
    public void Resolve_ShouldKeepOrderAddressForNonRotateProxy(ProxyOrderKind kind)
    {
        var address = RotateProxyAssignmentAddress.Resolve(
            kind,
            "1.2.3.4:8080:user:pass",
            "9.9.9.9:8080:user:pass");

        address.Should().Be("1.2.3.4:8080:user:pass");
    }
}
