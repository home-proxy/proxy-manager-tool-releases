using NetAgent.ProxyManager.Core.Models;

namespace NetAgent.ProxyManager.Infrastructure.Services;

public interface IProxifierLicenseStore
{
    ProxifierRegistrationState GetState();

    void Save(string owner, string key);
}
