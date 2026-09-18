using NetAgent.ProxyManager.Core.Models;

namespace NetAgent.ProxyManager.Core.Services;

public static class RunningApplicationRuleFactory
{
    public static IReadOnlyList<RunningApplicationCandidate> BuildCandidates(
        IReadOnlyList<EmulatorProcessCandidate> emulatorCandidates,
        IReadOnlyList<RunningProcessInfo> processes)
    {
        var emulatorProcessIds = emulatorCandidates
            .Select(candidate => candidate.ProcessId)
            .ToHashSet();
        var detectedEmulatorKinds = emulatorCandidates
            .Select(candidate => candidate.EmulatorKind)
            .ToHashSet();
        var processTargets = BuildProcessTargets(processes);
        var candidates = new List<RunningApplicationCandidate>();

        candidates.AddRange(emulatorCandidates.Select(candidate =>
        {
            var process = processes.FirstOrDefault(process => process.ProcessId == candidate.ProcessId);
            return new RunningApplicationCandidate(
                GetEmulatorKey(candidate),
                candidate.EmulatorInstanceName,
                process,
                candidate,
                [],
                SearchText: string.Join(" ", new[]
                {
                    candidate.EmulatorInstanceName,
                    candidate.EmulatorKind.ToString(),
                    candidate.ProcessName,
                    candidate.MainWindowTitle
                }.Where(value => !string.IsNullOrWhiteSpace(value))));
        }));

        var nonEmulatorProcesses = processes
            .Where(process => !emulatorProcessIds.Contains(process.ProcessId))
            .Where(process => !EmulatorLauncherProcesses.IsLauncherProcessName(detectedEmulatorKinds, process.ExecutableName))
            .ToList();

        candidates.AddRange(nonEmulatorProcesses
            .Where(process => IsUsableExecutablePath(process.ExecutablePath))
            .GroupBy(process => process.ExecutablePath!, StringComparer.OrdinalIgnoreCase)
            .Select(group => CreateExecutablePathCandidate(group.ToList(), processTargets)));

        return candidates
            .OrderByDescending(candidate => candidate.IsEmulator)
            .ThenBy(candidate => candidate.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(candidate => candidate.Process?.ProcessId ?? candidate.Emulator?.ProcessId ?? 0)
            .ToList();
    }

    private static RunningApplicationCandidate CreateExecutablePathCandidate(
        IReadOnlyList<RunningProcessInfo> processes,
        IReadOnlyDictionary<int, IReadOnlyList<string>> processTargets)
    {
        var first = processes
            .OrderByDescending(process => !string.IsNullOrWhiteSpace(process.MainWindowTitle))
            .ThenBy(process => process.ProcessId)
            .First();
        var targets = processes
            .SelectMany(process => processTargets.TryGetValue(process.ProcessId, out var items) ? items : [])
            .DefaultIfEmpty(first.ExecutablePath!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new RunningApplicationCandidate(
            GetProcessKey(first),
            FormatGroupedDisplayName(GetProcessDisplayName(first), processes.Count),
            first,
            Emulator: null,
            targets,
            ProcessCount: processes.Count,
            SearchText: GetProcessSearchText(processes));
    }

    private static string FormatGroupedDisplayName(string displayName, int processCount) =>
        processCount > 1 ? $"{displayName} ({processCount})" : displayName;

    public static RunningApplicationRuleSelectionResult CreateRules(
        IReadOnlyList<RunningApplicationCandidate> selectedCandidates)
    {
        var rules = new List<ApplicationRule>();
        var executablePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var emulatorKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var skipped = 0;

        foreach (var candidate in selectedCandidates)
        {
            if (candidate.Emulator is { } emulator)
            {
                var key = GetEmulatorKey(emulator);
                if (emulatorKeys.Add(key))
                {
                    rules.Add(new ApplicationRule
                    {
                        TargetType = ApplicationTargetType.Emulator,
                        ProcessId = emulator.ProcessId,
                        EmulatorKind = emulator.EmulatorKind,
                        EmulatorInstanceKey = emulator.EmulatorInstanceKey,
                        EmulatorInstanceName = emulator.EmulatorInstanceName,
                        RuntimeProcessName = emulator.ProcessName,
                        RuntimeExecutablePath = emulator.ExecutablePath,
                        IsEnabled = false
                    });
                }

                continue;
            }

            var paths = candidate.TargetExecutablePaths
                .Where(IsUsableExecutablePath)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (paths.Count == 0)
            {
                skipped++;
                continue;
            }

            foreach (var path in paths)
            {
                if (!executablePaths.Add(path))
                {
                    continue;
                }

                rules.Add(new ApplicationRule
                {
                    TargetType = ApplicationTargetType.Executable,
                    ExecutableName = path,
                    IsEnabled = false
                });
            }
        }

        return new RunningApplicationRuleSelectionResult(rules, skipped);
    }

    private static IReadOnlyDictionary<int, IReadOnlyList<string>> BuildProcessTargets(
        IReadOnlyList<RunningProcessInfo> processes)
    {
        var targets = new Dictionary<int, IReadOnlyList<string>>();
        var processesById = processes.ToDictionary(process => process.ProcessId);
        var networkProcesses = processes
            .Where(process => process.HasNetworkActivity && IsUsableExecutablePath(process.ExecutablePath))
            .ToList();

        foreach (var process in processes)
        {
            var processTargets = new List<string>();
            if (IsUsableExecutablePath(process.ExecutablePath))
            {
                processTargets.Add(process.ExecutablePath!);
            }

            if (!process.HasNetworkActivity)
            {
                processTargets.AddRange(networkProcesses
                    .Where(networkProcess => IsNetworkHelperForSelectedProcess(process, networkProcess, processesById))
                    .Select(networkProcess => networkProcess.ExecutablePath!)
                    .Distinct(StringComparer.OrdinalIgnoreCase));
            }

            targets[process.ProcessId] = processTargets
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        return targets;
    }

    private static bool IsNetworkHelperForSelectedProcess(
        RunningProcessInfo selectedProcess,
        RunningProcessInfo networkProcess,
        IReadOnlyDictionary<int, RunningProcessInfo> processesById)
    {
        if (selectedProcess.ProcessId == networkProcess.ProcessId)
        {
            return true;
        }

        return IsAncestor(selectedProcess.ProcessId, networkProcess, processesById) &&
            HasSameExecutableDirectory(selectedProcess, networkProcess);
    }

    private static bool HasSameExecutableDirectory(
        RunningProcessInfo selectedProcess,
        RunningProcessInfo networkProcess)
    {
        if (selectedProcess.ExecutablePath is not { } selectedPath ||
            networkProcess.ExecutablePath is not { } networkPath)
        {
            return false;
        }

        var selectedDirectory = Path.GetDirectoryName(selectedPath);
        var networkDirectory = Path.GetDirectoryName(networkPath);
        return !string.IsNullOrWhiteSpace(selectedDirectory) &&
            !string.IsNullOrWhiteSpace(networkDirectory) &&
            string.Equals(selectedDirectory, networkDirectory, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsAncestor(
        int ancestorProcessId,
        RunningProcessInfo process,
        IReadOnlyDictionary<int, RunningProcessInfo> processesById)
    {
        var seen = new HashSet<int>();
        var parentProcessId = process.ParentProcessId;

        while (parentProcessId is { } parent && seen.Add(parent))
        {
            if (parent == ancestorProcessId)
            {
                return true;
            }

            parentProcessId = processesById.TryGetValue(parent, out var parentProcess)
                ? parentProcess.ParentProcessId
                : null;
        }

        return false;
    }

    private static string GetProcessDisplayName(RunningProcessInfo process)
    {
        if (!string.IsNullOrWhiteSpace(process.MainWindowTitle))
        {
            return process.MainWindowTitle.Trim();
        }

        if (!string.IsNullOrWhiteSpace(process.ExecutablePath))
        {
            var fileName = Path.GetFileName(process.ExecutablePath);
            if (!string.IsNullOrWhiteSpace(fileName))
            {
                return fileName;
            }
        }

        return string.IsNullOrWhiteSpace(process.ExecutableName)
            ? process.DisplayName
            : process.ExecutableName;
    }

    private static string GetProcessSearchText(IReadOnlyList<RunningProcessInfo> processes) =>
        string.Join(
            " ",
            processes.SelectMany(process => new[]
                {
                    process.DisplayName,
                    process.ExecutableName,
                    process.ExecutablePath,
                    process.MainWindowTitle,
                    process.CommandLine
                })
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.OrdinalIgnoreCase));

    private static string GetProcessKey(RunningProcessInfo process) =>
        !string.IsNullOrWhiteSpace(process.ExecutablePath)
            ? $"process:path:{process.ExecutablePath}"
            : $"process:pid:{process.ProcessId}";

    private static string GetEmulatorKey(EmulatorProcessCandidate candidate) =>
        !string.IsNullOrWhiteSpace(candidate.EmulatorInstanceKey)
            ? $"emulator:{candidate.EmulatorKind}:{candidate.EmulatorInstanceKey}"
            : $"emulator:{candidate.EmulatorKind}:{candidate.ProcessId}";

    private static bool IsUsableExecutablePath(string? path) =>
        !string.IsNullOrWhiteSpace(path) &&
        string.Equals(Path.GetExtension(path), ".exe", StringComparison.OrdinalIgnoreCase);
}
