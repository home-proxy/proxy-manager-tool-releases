namespace NetAgent.ProxyManager.Infrastructure.Services;

public interface IProxifierOwnershipStore
{
    bool TryDetectInstalled(out ProxifierInstallMetadata metadata);

    void RecordInstalledByProxyManager(ProxifierInstallMetadata metadata);
}
