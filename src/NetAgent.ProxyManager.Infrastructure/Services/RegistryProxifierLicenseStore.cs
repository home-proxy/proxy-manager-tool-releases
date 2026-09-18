using System.Runtime.Versioning;
using Microsoft.Win32;
using NetAgent.ProxyManager.Core.Models;

namespace NetAgent.ProxyManager.Infrastructure.Services;

[SupportedOSPlatform("windows")]
public sealed class RegistryProxifierLicenseStore : IProxifierLicenseStore
{
    public const string LicenseRegistryPath = @"Software\Initex\Proxifier\License";
    public const string OwnerValueName = "Owner";
    public const string KeyValueName = "Key";

    public ProxifierRegistrationState GetState()
    {
        using var key = Registry.CurrentUser.OpenSubKey(LicenseRegistryPath, writable: false);
        var owner = key?.GetValue(OwnerValueName) as string;
        var registrationKey = key?.GetValue(KeyValueName) as string;
        return new ProxifierRegistrationState(
            !string.IsNullOrWhiteSpace(owner) && !string.IsNullOrWhiteSpace(registrationKey),
            owner);
    }

    public void Save(string owner, string key)
    {
        using var registryKey = Registry.CurrentUser.CreateSubKey(LicenseRegistryPath);
        registryKey.SetValue(OwnerValueName, owner, RegistryValueKind.String);
        registryKey.SetValue(KeyValueName, key, RegistryValueKind.String);
    }
}
