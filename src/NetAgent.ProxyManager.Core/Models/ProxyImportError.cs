namespace NetAgent.ProxyManager.Core.Models;

public sealed record ProxyImportError(int LineNumber, string RawLine, string Message);
