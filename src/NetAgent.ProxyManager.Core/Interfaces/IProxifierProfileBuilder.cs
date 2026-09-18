using NetAgent.ProxyManager.Core.Models;

namespace NetAgent.ProxyManager.Core.Interfaces;

public interface IProxifierProfileBuilder
{
    GeneratedProfileResult Build(ProxifierProfileModel model);
}
