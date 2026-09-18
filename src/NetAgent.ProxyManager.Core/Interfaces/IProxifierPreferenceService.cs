using NetAgent.ProxyManager.Core.Models;

namespace NetAgent.ProxyManager.Core.Interfaces;

public interface IProxifierPreferenceService
{
    ProxifierPreferenceResult ApplyEndUserDefaults();
}
