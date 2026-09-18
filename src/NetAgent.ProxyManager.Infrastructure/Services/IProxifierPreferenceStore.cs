namespace NetAgent.ProxyManager.Infrastructure.Services;

public interface IProxifierPreferenceStore
{
    void EnableSystemTrayIcon();

    void DisableAutostart();
}
