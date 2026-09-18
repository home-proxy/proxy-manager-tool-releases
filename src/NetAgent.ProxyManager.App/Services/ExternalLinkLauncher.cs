using System.Diagnostics;

namespace NetAgent.ProxyManager.App.Services;

internal static class ExternalLinkLauncher
{
    public static void OpenInChromeOrDefault(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "chrome.exe",
                Arguments = url,
                UseShellExecute = true
            });
            return;
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception or FileNotFoundException)
        {
        }

        Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
    }
}
