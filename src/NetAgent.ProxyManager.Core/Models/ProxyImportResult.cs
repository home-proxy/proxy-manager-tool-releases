namespace NetAgent.ProxyManager.Core.Models;

public sealed record ProxyImportResult(
    IReadOnlyList<ProxyServer> Proxies,
    IReadOnlyList<ProxyImportError> Errors)
{
    public bool HasErrors => Errors.Count > 0;
}
