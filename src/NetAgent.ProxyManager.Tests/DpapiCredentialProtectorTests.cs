using FluentAssertions;
using NetAgent.ProxyManager.Core.Interfaces;
using NetAgent.ProxyManager.Core.Services;

namespace NetAgent.ProxyManager.Tests;

public sealed class DpapiCredentialProtectorTests
{
    [Fact]
    public void DpapiCredentialProtector_ShouldImplementCredentialProtectorContract()
    {
        typeof(DpapiCredentialProtector).Should().Implement<ICredentialProtector>();
    }

    [Fact]
    public void ProtectAndUnprotect_ShouldRoundTripOnWindows()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        ICredentialProtector protector = new DpapiCredentialProtector();
        var protectedValue = protector.Protect("secret-value");

        protectedValue.Should().NotBe("secret-value");
        protector.Unprotect(protectedValue).Should().Be("secret-value");
    }
}
