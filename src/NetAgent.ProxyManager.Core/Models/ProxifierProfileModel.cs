namespace NetAgent.ProxyManager.Core.Models;

public sealed class ProxifierProfileModel
{
    public required IReadOnlyList<ProxyServer> Proxies { get; init; }
    public required IReadOnlyList<ApplicationRule> Rules { get; init; }
    public bool DefaultRouteDirect { get; init; } = true;
}
