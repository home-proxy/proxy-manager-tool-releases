namespace NetAgent.ProxyManager.Core.Models;

public sealed record EmulatorProcessCandidate(
    int ProcessId,
    int? ParentProcessId,
    EmulatorKind EmulatorKind,
    string EmulatorInstanceKey,
    string EmulatorInstanceName,
    string RuntimeType,
    string ProcessName,
    string? ExecutablePath,
    string? CommandLine,
    string? MainWindowTitle);
