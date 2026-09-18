using NetAgent.ProxyManager.Core.Models;

namespace NetAgent.ProxyManager.Core.Interfaces;

public interface IBulkProxyParser
{
    ProxyImportResult Parse(string input);
}
