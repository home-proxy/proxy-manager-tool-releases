using System.Runtime.Versioning;
using Microsoft.Win32;

namespace NetAgent.ProxyManager.Infrastructure.Services;

[SupportedOSPlatform("windows")]
public sealed class RegistryProxifierPreferenceStore : IProxifierPreferenceStore
{
    private const string ProxifierSettingsPath = @"Software\Initex\Proxifier\Settings";
    private const string RunPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string RunOncePath = @"Software\Microsoft\Windows\CurrentVersion\RunOnce";

    public void EnableSystemTrayIcon()
    {
        using var settingsKey = Registry.CurrentUser.CreateSubKey(ProxifierSettingsPath);
        settingsKey.SetValue("SysTrayIcon", 1, RegistryValueKind.DWord);
        settingsKey.SetValue("SysTrayIconShowTraffic", 0, RegistryValueKind.DWord);
    }

    public void DisableAutostart()
    {
        DeleteProxifierRunEntries(Registry.CurrentUser, RunPath);
        DeleteProxifierRunEntries(Registry.CurrentUser, RunOncePath);

        TryDeleteProxifierRunEntries(Registry.LocalMachine, RunPath);
        TryDeleteProxifierRunEntries(Registry.LocalMachine, RunOncePath);
        TryDeleteProxifierRunEntries(Registry.LocalMachine, @"Software\WOW6432Node\Microsoft\Windows\CurrentVersion\Run");
        TryDeleteProxifierRunEntries(Registry.LocalMachine, @"Software\WOW6432Node\Microsoft\Windows\CurrentVersion\RunOnce");

        DeleteProxifierStartupShortcuts(
            Environment.GetFolderPath(Environment.SpecialFolder.Startup));
        TryDeleteProxifierStartupShortcuts(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonStartup));
    }

    private static void TryDeleteProxifierRunEntries(RegistryKey root, string path)
    {
        try
        {
            DeleteProxifierRunEntries(root, path);
        }
        catch (UnauthorizedAccessException)
        {
        }
        catch (IOException)
        {
        }
        catch (System.Security.SecurityException)
        {
        }
    }

    private static void DeleteProxifierRunEntries(RegistryKey root, string path)
    {
        using var key = root.OpenSubKey(path, writable: true);
        if (key is null)
        {
            return;
        }

        foreach (var valueName in key.GetValueNames())
        {
            var value = key.GetValue(valueName)?.ToString() ?? string.Empty;
            if (IsProxifierAutostartEntry(valueName, value))
            {
                key.DeleteValue(valueName, throwOnMissingValue: false);
            }
        }
    }

    private static bool IsProxifierAutostartEntry(string valueName, string value) =>
        valueName.Contains("Proxifier", StringComparison.OrdinalIgnoreCase) ||
        value.Contains("Proxifier.exe", StringComparison.OrdinalIgnoreCase);

    private static void TryDeleteProxifierStartupShortcuts(string startupDirectory)
    {
        try
        {
            DeleteProxifierStartupShortcuts(startupDirectory);
        }
        catch (UnauthorizedAccessException)
        {
        }
        catch (IOException)
        {
        }
    }

    private static void DeleteProxifierStartupShortcuts(string startupDirectory)
    {
        if (string.IsNullOrWhiteSpace(startupDirectory) || !Directory.Exists(startupDirectory))
        {
            return;
        }

        foreach (var shortcutPath in Directory.EnumerateFiles(startupDirectory, "Proxifier*.lnk", SearchOption.TopDirectoryOnly))
        {
            File.Delete(shortcutPath);
        }
    }
}
