namespace NetAgent.ProxyManager.Core.Models;

public sealed record ProxifierPreferenceResult(
    bool SystemTrayIconEnabled,
    bool AutostartDisabled,
    IReadOnlyList<string> Warnings);
