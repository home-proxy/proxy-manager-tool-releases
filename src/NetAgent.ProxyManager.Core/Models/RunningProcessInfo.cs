namespace NetAgent.ProxyManager.Core.Models;

public sealed record RunningProcessInfo(
    int ProcessId,
    string ExecutableName,
    string DisplayName,
    string? MainWindowTitle,
    string? ExecutablePath = null,
    int? ParentProcessId = null,
    string? CommandLine = null,
    bool HasNetworkActivity = false);
