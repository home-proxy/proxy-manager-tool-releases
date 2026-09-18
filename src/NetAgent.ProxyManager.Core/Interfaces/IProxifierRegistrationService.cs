using NetAgent.ProxyManager.Core.Models;

namespace NetAgent.ProxyManager.Core.Interfaces;

public interface IProxifierRegistrationService
{
    ProxifierRegistrationState GetRegistrationState();

    void SaveRegistration(string owner, string key);

    void SaveRegistrationKey(string key);

    bool TrySaveConfiguredRegistration();
}
