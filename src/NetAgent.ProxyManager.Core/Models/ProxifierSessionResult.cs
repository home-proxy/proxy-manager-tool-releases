namespace NetAgent.ProxyManager.Core.Models;

public sealed record ProxifierSessionResult(
    bool Success,
    string Message,
    string ProfilePath,
    GeneratedProfileResult Profile);
