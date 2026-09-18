using NetAgent.ProxyManager.Core.Models;

namespace NetAgent.ProxyManager.Core.Interfaces;

public interface IProxifierInstallerService
{
    bool TryResolveBundledInstallerPath(out string installerPath);

    Task<ProxifierInstallResult> InstallOrRepairAsync(CancellationToken cancellationToken);
}
