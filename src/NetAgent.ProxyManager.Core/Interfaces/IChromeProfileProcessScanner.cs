using NetAgent.ProxyManager.Core.Models;

namespace NetAgent.ProxyManager.Core.Interfaces;

public interface IChromeProfileProcessScanner
{
    Task<IReadOnlyList<ChromeProfileRuntimeInfo>> ScanAsync(
        IReadOnlyCollection<string> browserExecutablePaths,
        CancellationToken cancellationToken);
}
