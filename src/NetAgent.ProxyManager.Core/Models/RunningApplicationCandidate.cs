namespace NetAgent.ProxyManager.Core.Models;

public sealed record RunningApplicationCandidate(
    string Key,
    string DisplayName,
    RunningProcessInfo? Process,
    EmulatorProcessCandidate? Emulator,
    IReadOnlyList<string> TargetExecutablePaths,
    int ProcessCount = 1,
    string? SearchText = null)
{
    public bool IsEmulator => Emulator is not null;
    public bool HasExecutableTarget => TargetExecutablePaths.Count > 0;
    public string EffectiveSearchText => string.IsNullOrWhiteSpace(SearchText) ? DisplayName : SearchText;
}

public sealed record RunningApplicationRuleSelectionResult(
    IReadOnlyList<ApplicationRule> Rules,
    int SkippedCount);
