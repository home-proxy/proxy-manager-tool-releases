namespace NetAgent.ProxyManager.Core.Models;

public sealed class ProxifierProfileModel
{
    public required IReadOnlyList<ProxyServer> Proxies { get; init; }
    public required IReadOnlyList<ApplicationRule> Rules { get; init; }
    public bool DefaultRouteDirect { get; init; } = true;

    // Executables that must never reach the network directly (the tool-owned Chrome). Their
    // processes are routed only by the pid rules above; anything not matched — e.g. a Chrome
    // NetworkService that just restarted with a new PID — is blocked instead of leaking the real IP.
    public IReadOnlyList<string> BlockedExecutablePaths { get; init; } = [];
}
