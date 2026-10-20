using NetAgent.ProxyManager.Core.Models;

namespace NetAgent.ProxyManager.Core.Interfaces;

// Owns the tool's private Chrome build. It must live at its own path (never the user's installed
// Chrome) because the Proxifier leak-guard rule blocks this executable by path.
public interface IChromeBrowserProvisioner
{
    bool IsInstalled { get; }

    string BrowserExecutablePath { get; }

    // Every tool Chrome executable present on disk (current and older versions), so that the
    // leak guard and the process scanner also cover an instance still running an older version.
    IReadOnlyList<string> GetInstalledExecutablePaths();

    Task<string> EnsureInstalledAsync(IProgress<ChromeBrowserDownloadProgress>? progress, CancellationToken cancellationToken);
}
