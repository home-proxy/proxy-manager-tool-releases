using FluentAssertions;
using NetAgent.ProxyManager.Infrastructure.Services;

namespace NetAgent.ProxyManager.Tests;

public sealed class ProxifierPreferenceServiceTests
{
    [Fact]
    public void ApplyEndUserDefaults_ShouldEnableSystemTrayIconAndDisableAutostart()
    {
        var store = new FakePreferenceStore();
        var service = new ProxifierPreferenceService(store);

        var result = service.ApplyEndUserDefaults();

        result.SystemTrayIconEnabled.Should().BeTrue();
        result.AutostartDisabled.Should().BeTrue();
        result.Warnings.Should().BeEmpty();
        store.EnableSystemTrayIconCalls.Should().Be(1);
        store.DisableAutostartCalls.Should().Be(1);
    }

    [Fact]
    public void ApplyEndUserDefaults_ShouldReturnWarningsWithoutThrowing()
    {
        var store = new FakePreferenceStore
        {
            ThrowOnSystemTrayIcon = true,
            ThrowOnAutostart = true
        };
        var service = new ProxifierPreferenceService(store);

        var result = service.ApplyEndUserDefaults();

        result.SystemTrayIconEnabled.Should().BeFalse();
        result.AutostartDisabled.Should().BeFalse();
        result.Warnings.Should().HaveCount(2);
    }

    private sealed class FakePreferenceStore : IProxifierPreferenceStore
    {
        public bool ThrowOnSystemTrayIcon { get; init; }
        public bool ThrowOnAutostart { get; init; }
        public int EnableSystemTrayIconCalls { get; private set; }
        public int DisableAutostartCalls { get; private set; }

        public void EnableSystemTrayIcon()
        {
            EnableSystemTrayIconCalls++;
            if (ThrowOnSystemTrayIcon)
            {
                throw new UnauthorizedAccessException("registry denied");
            }
        }

        public void DisableAutostart()
        {
            DisableAutostartCalls++;
            if (ThrowOnAutostart)
            {
                throw new UnauthorizedAccessException("startup denied");
            }
        }
    }
}
