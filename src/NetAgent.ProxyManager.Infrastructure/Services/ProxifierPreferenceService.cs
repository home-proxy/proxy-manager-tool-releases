using NetAgent.ProxyManager.Core.Interfaces;
using NetAgent.ProxyManager.Core.Models;

namespace NetAgent.ProxyManager.Infrastructure.Services;

public sealed class ProxifierPreferenceService(IProxifierPreferenceStore preferenceStore) : IProxifierPreferenceService
{
    public ProxifierPreferenceResult ApplyEndUserDefaults()
    {
        var systemTrayIconEnabled = false;
        var autostartDisabled = false;
        var warnings = new List<string>();

        try
        {
            preferenceStore.EnableSystemTrayIcon();
            systemTrayIconEnabled = true;
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException or System.ComponentModel.Win32Exception)
        {
            warnings.Add($"Không thể bật icon system tray: {ex.Message}");
        }

        try
        {
            preferenceStore.DisableAutostart();
            autostartDisabled = true;
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException or System.ComponentModel.Win32Exception)
        {
            warnings.Add($"Không thể tắt autostart: {ex.Message}");
        }

        return new ProxifierPreferenceResult(systemTrayIconEnabled, autostartDisabled, warnings);
    }
}

