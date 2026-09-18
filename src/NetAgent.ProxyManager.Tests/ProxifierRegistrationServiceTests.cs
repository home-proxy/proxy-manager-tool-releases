using FluentAssertions;
using Microsoft.Extensions.Options;
using NetAgent.ProxyManager.Core.Models;
using NetAgent.ProxyManager.Infrastructure.Api;
using NetAgent.ProxyManager.Infrastructure.Services;

namespace NetAgent.ProxyManager.Tests;

public sealed class ProxifierRegistrationServiceTests
{
    [Fact]
    public void SaveRegistration_ShouldTrimOwnerAndKey()
    {
        var store = new FakeLicenseStore();
        var service = CreateService(store);

        service.SaveRegistration("  owner  ", "  key  ");

        store.Owner.Should().Be("owner");
        store.Key.Should().Be("key");
    }

    [Theory]
    [InlineData("", "key")]
    [InlineData("owner", "")]
    public void SaveRegistration_ShouldRejectMissingValues(string owner, string key)
    {
        var service = CreateService(new FakeLicenseStore());

        var act = () => service.SaveRegistration(owner, key);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void GetRegistrationState_ShouldReturnStoreState()
    {
        var store = new FakeLicenseStore
        {
            State = new ProxifierRegistrationState(true, "owner")
        };
        var service = CreateService(store);

        var state = service.GetRegistrationState();

        state.Should().Be(new ProxifierRegistrationState(true, "owner"));
    }

    [Fact]
    public void TrySaveConfiguredRegistration_ShouldSaveConfiguredOwnerAndKey()
    {
        var store = new FakeLicenseStore();
        var service = CreateService(store, new ProxifierOptions
        {
            RegistrationOwner = "  configured-owner  ",
            RegistrationKey = "  configured-key  "
        });

        var result = service.TrySaveConfiguredRegistration();

        result.Should().BeTrue();
        store.Owner.Should().Be("configured-owner");
        store.Key.Should().Be("configured-key");
    }

    [Fact]
    public void TrySaveConfiguredRegistration_ShouldReturnFalseWhenConfigIsMissing()
    {
        var store = new FakeLicenseStore();
        var service = CreateService(store);

        var result = service.TrySaveConfiguredRegistration();

        result.Should().BeFalse();
        store.Owner.Should().BeNull();
        store.Key.Should().BeNull();
    }

    [Fact]
    public void SaveRegistrationKey_ShouldUseConfiguredOwner()
    {
        var store = new FakeLicenseStore();
        var service = CreateService(store, new ProxifierOptions
        {
            RegistrationOwner = "configured-owner"
        });

        service.SaveRegistrationKey(" manual-key ");

        store.Owner.Should().Be("configured-owner");
        store.Key.Should().Be("manual-key");
    }

    private static ProxifierRegistrationService CreateService(
        FakeLicenseStore store,
        ProxifierOptions? options = null) =>
        new(store, Options.Create(options ?? new ProxifierOptions()));

    private sealed class FakeLicenseStore : IProxifierLicenseStore
    {
        public ProxifierRegistrationState State { get; set; } = new(false, null);
        public string? Owner { get; private set; }
        public string? Key { get; private set; }

        public ProxifierRegistrationState GetState() => State;

        public void Save(string owner, string key)
        {
            Owner = owner;
            Key = key;
        }
    }
}
