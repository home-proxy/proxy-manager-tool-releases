using NetAgent.ProxyManager.Core.Interfaces;
using NetAgent.ProxyManager.Core.Models;
using NetAgent.ProxyManager.Infrastructure.Api;
using Microsoft.Extensions.Options;

namespace NetAgent.ProxyManager.Infrastructure.Services;

public sealed class ProxifierRegistrationService(
    IProxifierLicenseStore licenseStore,
    IOptions<ProxifierOptions> proxifierOptions) : IProxifierRegistrationService
{
    public ProxifierRegistrationState GetRegistrationState() => licenseStore.GetState();

    public void SaveRegistration(string owner, string key)
    {
        if (string.IsNullOrWhiteSpace(owner))
        {
            throw new ArgumentException("Owner không được để trống.", nameof(owner));
        }

        if (string.IsNullOrWhiteSpace(key))
        {
            throw new ArgumentException("Registration key không được để trống.", nameof(key));
        }

        licenseStore.Save(owner.Trim(), key.Trim());
    }

    public void SaveRegistrationKey(string key)
    {
        var owner = FirstNonEmpty(
            proxifierOptions.Value.RegistrationOwner,
            GetRegistrationState().Owner,
            "ProxyManager");
        SaveRegistration(owner, key);
    }

    public bool TrySaveConfiguredRegistration()
    {
        var owner = proxifierOptions.Value.RegistrationOwner;
        var key = proxifierOptions.Value.RegistrationKey;
        if (string.IsNullOrWhiteSpace(owner) || string.IsNullOrWhiteSpace(key))
        {
            return false;
        }

        SaveRegistration(owner, key);
        return true;
    }

    private static string FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim() ?? string.Empty;
}
