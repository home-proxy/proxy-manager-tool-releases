using System.Runtime.Versioning;
using Microsoft.Win32;

namespace NetAgent.ProxyManager.Infrastructure.Services;

[SupportedOSPlatform("windows")]
public sealed class RegistryProxifierOwnershipStore : IProxifierOwnershipStore
{
    private const string ProxyManagerRegistryPath = @"Software\ProxyManager";
    private const string ProxifierOwnershipValueName = "ProxifierInstalledByProxyManager";
    private const string ProxifierUninstallValueName = "ProxifierUninstallString";
    private const string ProxifierExePathValueName = "ProxifierExePath";
    private const string UninstallRegistryPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall";

    private static readonly string[] CommonInstallPaths =
    [
        @"C:\Program Files (x86)\Proxifier\Proxifier.exe",
        @"C:\Program Files\Proxifier\Proxifier.exe"
    ];

    public bool TryDetectInstalled(out ProxifierInstallMetadata metadata)
    {
        if (TryDetectInstalledInRegistry(RegistryView.Registry64, out metadata) ||
            TryDetectInstalledInRegistry(RegistryView.Registry32, out metadata))
        {
            return true;
        }

        var path = CommonInstallPaths.FirstOrDefault(File.Exists);
        if (!string.IsNullOrWhiteSpace(path))
        {
            metadata = new ProxifierInstallMetadata(path, null);
            return true;
        }

        metadata = new ProxifierInstallMetadata(null, null);
        return false;
    }

    public void RecordInstalledByProxyManager(ProxifierInstallMetadata metadata)
    {
        if (string.IsNullOrWhiteSpace(metadata.UninstallString))
        {
            throw new InvalidOperationException("Cannot record Proxifier ownership without an uninstall command.");
        }

        using var root = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
        using var key = root.CreateSubKey(ProxyManagerRegistryPath);
        key.SetValue(ProxifierOwnershipValueName, "1", RegistryValueKind.String);
        key.SetValue(ProxifierUninstallValueName, metadata.UninstallString.Trim(), RegistryValueKind.String);

        if (!string.IsNullOrWhiteSpace(metadata.ProxifierExecutablePath))
        {
            key.SetValue(ProxifierExePathValueName, metadata.ProxifierExecutablePath.Trim(), RegistryValueKind.String);
        }
    }

    private static bool TryDetectInstalledInRegistry(
        RegistryView registryView,
        out ProxifierInstallMetadata metadata)
    {
        try
        {
            using var root = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, registryView);
            using var uninstallRoot = root.OpenSubKey(UninstallRegistryPath, writable: false);
            if (uninstallRoot is null)
            {
                metadata = new ProxifierInstallMetadata(null, null);
                return false;
            }

            foreach (var subkeyName in uninstallRoot.GetSubKeyNames())
            {
                using var appKey = uninstallRoot.OpenSubKey(subkeyName, writable: false);
                var displayName = appKey?.GetValue("DisplayName") as string;
                if (!IsProxifierDisplayName(displayName))
                {
                    continue;
                }

                metadata = new ProxifierInstallMetadata(
                    ResolveProxifierExecutablePath(appKey),
                    ResolveUninstallString(appKey));
                return true;
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
        }

        metadata = new ProxifierInstallMetadata(null, null);
        return false;
    }

    private static bool IsProxifierDisplayName(string? displayName)
    {
        if (string.IsNullOrWhiteSpace(displayName))
        {
            return false;
        }

        return displayName.Contains("Proxifier", StringComparison.OrdinalIgnoreCase) &&
            (displayName.Contains("Standard", StringComparison.OrdinalIgnoreCase) ||
             string.Equals(displayName, "Proxifier", StringComparison.OrdinalIgnoreCase));
    }

    private static string? ResolveUninstallString(RegistryKey? appKey) =>
        FirstNonEmpty(
            appKey?.GetValue("QuietUninstallString") as string,
            appKey?.GetValue("UninstallString") as string);

    private static string? ResolveProxifierExecutablePath(RegistryKey? appKey)
    {
        var installLocation = appKey?.GetValue("InstallLocation") as string;
        if (!string.IsNullOrWhiteSpace(installLocation))
        {
            var candidate = Path.Combine(installLocation.Trim(), "Proxifier.exe");
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        var displayIcon = appKey?.GetValue("DisplayIcon") as string;
        return NormalizeDisplayIconPath(displayIcon);
    }

    private static string? NormalizeDisplayIconPath(string? displayIcon)
    {
        if (string.IsNullOrWhiteSpace(displayIcon))
        {
            return null;
        }

        var path = displayIcon.Trim().Trim('"');
        var commaIndex = path.IndexOf(',', StringComparison.Ordinal);
        return commaIndex > 0 ? path[..commaIndex] : path;
    }

    private static string? FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim();
}
