using System.Management;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using System.Runtime.Versioning;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using NetAgent.ProxyManager.Core.Interfaces;
using NetAgent.ProxyManager.Core.Models;

namespace NetAgent.ProxyManager.Core.Services;

[SupportedOSPlatform("windows")]
public sealed partial class EmulatorProcessScanner : IEmulatorProcessScanner
{
    public Task<IReadOnlyList<EmulatorProcessCandidate>> ScanAsync(
        IReadOnlyCollection<EmulatorKind> emulatorKinds,
        CancellationToken cancellationToken)
    {
        if (emulatorKinds.Count == 0)
        {
            return Task.FromResult<IReadOnlyList<EmulatorProcessCandidate>>([]);
        }

        var selected = emulatorKinds.ToHashSet();
        var candidates = new List<EmulatorProcessCandidate>();

        using var searcher = new ManagementObjectSearcher(
            "SELECT ProcessId,ParentProcessId,Name,ExecutablePath,CommandLine FROM Win32_Process");

        foreach (ManagementObject process in searcher.Get())
        {
            cancellationToken.ThrowIfCancellationRequested();

            var name = Convert.ToString(process["Name"]) ?? string.Empty;
            var executablePath = Convert.ToString(process["ExecutablePath"]);
            var commandLine = Convert.ToString(process["CommandLine"]);
            if (!TryClassifyRuntimeProcess(name, executablePath, commandLine, out var kind, out var runtimeType) ||
                !selected.Contains(kind))
            {
                continue;
            }

            var processId = Convert.ToInt32(process["ProcessId"]);
            var parentProcessId = process["ParentProcessId"] is null
                ? (int?)null
                : Convert.ToInt32(process["ParentProcessId"]);
            var mainWindowTitle = GetMainWindowTitle(processId);
            var instanceKey = ResolveInstanceKey(kind, executablePath, commandLine, mainWindowTitle, processId);
            var instanceName = ResolveInstanceName(kind, instanceKey, executablePath, commandLine, mainWindowTitle, processId);

            candidates.Add(new EmulatorProcessCandidate(
                processId,
                parentProcessId,
                kind,
                instanceKey,
                instanceName,
                runtimeType,
                name,
                executablePath,
                commandLine,
                mainWindowTitle));
        }

        IReadOnlyList<EmulatorProcessCandidate> result = candidates
            .OrderBy(candidate => candidate.EmulatorKind)
            .ThenBy(candidate => candidate.EmulatorInstanceName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(candidate => candidate.ProcessName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(candidate => candidate.ProcessId)
            .ToList();

        return Task.FromResult(result);
    }

    private static bool TryClassifyRuntimeProcess(
        string processName,
        string? executablePath,
        string? commandLine,
        out EmulatorKind kind,
        out string runtimeType)
    {
        var name = processName.Trim();
        var path = executablePath ?? string.Empty;
        var command = commandLine ?? string.Empty;

        if (name.Equals("HD-Player.exe", StringComparison.OrdinalIgnoreCase))
        {
            kind = EmulatorKind.BlueStacks;
            runtimeType = "Runtime";
            return true;
        }

        if (name.Equals("LdVBoxHeadless.exe", StringComparison.OrdinalIgnoreCase) ||
            name.Equals("Ld9BoxHeadless.exe", StringComparison.OrdinalIgnoreCase) ||
            name.Equals("VBoxHeadless.exe", StringComparison.OrdinalIgnoreCase) &&
            ContainsAny(path + " " + command, "LDPlayer", "leidian"))
        {
            kind = EmulatorKind.LDPlayer;
            runtimeType = "Headless";
            return true;
        }

        if (name.Equals("MEmuHeadless.exe", StringComparison.OrdinalIgnoreCase))
        {
            kind = EmulatorKind.MEmu;
            runtimeType = "Headless";
            return true;
        }

        if (name.Equals("NOXVM.exe", StringComparison.OrdinalIgnoreCase) ||
            name.Equals("NoxVMHandle.exe", StringComparison.OrdinalIgnoreCase))
        {
            kind = EmulatorKind.Nox;
            runtimeType = "Headless";
            return true;
        }

        kind = default;
        runtimeType = string.Empty;
        return false;
    }

    private static string ResolveInstanceKey(
        EmulatorKind kind,
        string? executablePath,
        string? commandLine,
        string? mainWindowTitle,
        int processId)
    {
        var command = commandLine ?? string.Empty;
        var path = executablePath ?? string.Empty;

        var fromOption = GetPreferredInstanceKey(kind, command);
        if (!string.IsNullOrWhiteSpace(fromOption))
        {
            return NormalizeInstanceKey(fromOption);
        }

        if (kind == EmulatorKind.BlueStacks)
        {
            var fromTitle = ResolveBlueStacksInstanceKeyFromDisplayName(mainWindowTitle);
            if (!string.IsNullOrWhiteSpace(fromTitle))
            {
                return fromTitle;
            }
        }

        var fromPath = kind switch
        {
            EmulatorKind.BlueStacks => FindBlueStacksInstanceKey(command),
            EmulatorKind.LDPlayer => FindPathSegment(path + " " + command, @"\\vms\\(?<value>leidian[^\\\s""]+)"),
            EmulatorKind.MEmu => FindPathSegment(path + " " + command, @"\\MemuHyperv VMs\\(?<value>[^\\\s""]+)"),
            EmulatorKind.Nox => FindPathSegment(path + " " + command, @"\\BignoxVMS\\(?<value>[^\\\s""]+)"),
            _ => null
        };

        if (!string.IsNullOrWhiteSpace(fromPath))
        {
            return NormalizeInstanceKey(fromPath);
        }

        return $"{kind}:{processId}";
    }

    private static string ResolveInstanceName(
        EmulatorKind kind,
        string instanceKey,
        string? executablePath,
        string? commandLine,
        string? mainWindowTitle,
        int processId)
    {
        var command = commandLine ?? string.Empty;

        return kind switch
        {
            EmulatorKind.BlueStacks => ResolveBlueStacksName(instanceKey, executablePath) ?? NormalizeBlueStacksWindowTitle(mainWindowTitle) ?? $"BlueStacks - {instanceKey}",
            EmulatorKind.LDPlayer => ResolveLDPlayerName(instanceKey, executablePath, command),
            EmulatorKind.MEmu => ResolveMEmuName(instanceKey, executablePath, command, processId),
            EmulatorKind.Nox => ResolveNoxName(instanceKey, executablePath, command),
            _ => instanceKey
        };
    }

    private static string? ResolveBlueStacksName(string instanceKey, string? executablePath)
    {
        foreach (var configPath in EnumerateBlueStacksConfigPaths(executablePath))
        {
            var key = $"bst.instance.{instanceKey}.display_name";
            foreach (var line in File.ReadLines(configPath))
            {
                if (!line.StartsWith(key, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var value = line[(line.IndexOf('=') + 1)..].Trim().Trim('"');
                return string.IsNullOrWhiteSpace(value) ? null : value;
            }
        }

        return null;
    }

    private static string? ResolveBlueStacksInstanceKeyFromDisplayName(string? mainWindowTitle)
    {
        var title = NormalizeBlueStacksWindowTitle(mainWindowTitle);
        if (string.IsNullOrWhiteSpace(title))
        {
            return null;
        }

        foreach (var configPath in EnumerateBlueStacksConfigPaths(executablePath: null))
        {
            foreach (var line in File.ReadLines(configPath))
            {
                var match = BlueStacksDisplayNameRegex().Match(line);
                if (!match.Success)
                {
                    continue;
                }

                var displayName = match.Groups["display"].Value.Trim();
                if (string.Equals(displayName, title, StringComparison.OrdinalIgnoreCase))
                {
                    return match.Groups["key"].Value.Trim();
                }
            }
        }

        return title;
    }

    private static IEnumerable<string> EnumerateBlueStacksConfigPaths(string? executablePath)
    {
        var candidates = new List<string>();
        var directory = GetExistingDirectory(executablePath);
        while (!string.IsNullOrWhiteSpace(directory))
        {
            candidates.Add(Path.Combine(directory, "bluestacks.conf"));
            directory = Directory.GetParent(directory)?.FullName;
        }

        candidates.AddRange(GetBlueStacksRegistryDirectories().Select(path => Path.Combine(path, "bluestacks.conf")));
        candidates.AddRange(GetFixedDriveBlueStacksConfigPaths());

        foreach (var candidate in candidates
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (File.Exists(candidate))
            {
                yield return candidate;
            }
        }
    }

    private static IEnumerable<string> GetBlueStacksRegistryDirectories()
    {
        const string subKey = @"SOFTWARE\BlueStacks_nxt";
        foreach (var root in new[] { Registry.LocalMachine, Registry.CurrentUser })
        {
            using var key = root.OpenSubKey(subKey);
            if (key is null)
            {
                continue;
            }

            foreach (var valueName in new[] { "UserDefinedDir", "DataDir", "LogDir", "InstallDir" })
            {
                if (key.GetValue(valueName) is not string value || string.IsNullOrWhiteSpace(value))
                {
                    continue;
                }

                yield return value.TrimEnd('\\');
                var parent = Directory.GetParent(value.TrimEnd('\\'));
                if (parent is not null)
                {
                    yield return parent.FullName;
                }
            }
        }
    }

    private static IEnumerable<string> GetFixedDriveBlueStacksConfigPaths()
    {
        foreach (var drive in DriveInfo.GetDrives().Where(drive => drive.DriveType == DriveType.Fixed && drive.IsReady))
        {
            foreach (var relativePath in new[]
            {
                @"emulator\BlueStacks_nxt\bluestacks.conf",
                @"BlueStacks_nxt\bluestacks.conf",
                @"Program Files\BlueStacks_nxt\bluestacks.conf",
                @"Program Files (x86)\BlueStacks_nxt\bluestacks.conf"
            })
            {
                yield return Path.Combine(drive.RootDirectory.FullName, relativePath);
            }
        }
    }

    private static string? NormalizeBlueStacksWindowTitle(string? mainWindowTitle)
    {
        if (string.IsNullOrWhiteSpace(mainWindowTitle))
        {
            return null;
        }

        var title = mainWindowTitle.Trim();
        return title.Equals("BlueStacks App Player", StringComparison.OrdinalIgnoreCase) ||
            title.Equals("BlueStacks", StringComparison.OrdinalIgnoreCase)
            ? null
            : title;
    }

    private static string ResolveLDPlayerName(string instanceKey, string? executablePath, string commandLine)
    {
        var configName = ResolveLDPlayerNameFromConfig(instanceKey, executablePath, commandLine);
        if (!string.IsNullOrWhiteSpace(configName))
        {
            return configName;
        }

        var match = LDPlayerIndexRegex().Match(instanceKey);
        if (match.Success && int.TryParse(match.Groups["index"].Value, out var index))
        {
            return $"LDPlayer-{index + 1}";
        }

        return FormatFallbackInstanceName("LDPlayer", instanceKey);
    }

    private static string? ResolveLDPlayerNameFromConfig(string instanceKey, string? executablePath, string commandLine)
    {
        var keys = new[]
            {
                instanceKey,
                GetCommandOptionValue(commandLine, "comment"),
                GetCommandOptionValue(commandLine, "instance", "vm-name", "name"),
                GetCommandOptionValue(commandLine, "startvm")
            }
            .Where(key => !string.IsNullOrWhiteSpace(key))
            .Select(key => NormalizeInstanceKey(key!))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var configDirectory in EnumerateLDPlayerConfigDirectories(executablePath, commandLine))
        {
            foreach (var key in keys)
            {
                var directConfigPath = Path.Combine(configDirectory, $"{key}.config");
                var directName = TryReadLDPlayerName(directConfigPath);
                if (!string.IsNullOrWhiteSpace(directName))
                {
                    return directName;
                }
            }

            var vmDirectory = Directory.GetParent(configDirectory)?.FullName;
            if (string.IsNullOrWhiteSpace(vmDirectory) || !Directory.Exists(vmDirectory))
            {
                continue;
            }

            foreach (var vboxPath in EnumerateFiles(vmDirectory, "*.vbox", SearchOption.AllDirectories))
            {
                var config = TryReadVirtualMachineConfig(vboxPath);
                if (config is null || !keys.Any(key => IsVirtualMachineConfigMatch(config, key)))
                {
                    continue;
                }

                var vmName = FirstNonEmpty(config.MachineName, Path.GetFileName(Path.GetDirectoryName(config.Path)));
                if (string.IsNullOrWhiteSpace(vmName))
                {
                    continue;
                }

                var mappedName = TryReadLDPlayerName(Path.Combine(configDirectory, $"{vmName}.config"));
                if (!string.IsNullOrWhiteSpace(mappedName))
                {
                    return mappedName;
                }
            }
        }

        return null;
    }

    private static string ResolveMEmuName(string instanceKey, string? executablePath, string commandLine, int processId)
    {
        var configPaths = EnumerateMEmuVirtualMachineConfigFiles(executablePath, commandLine);
        var name = ResolveMEmuNameFromConfigFiles(
            instanceKey,
            configPaths,
            GetTcpListeningPortsByProcessId(processId));

        return !string.IsNullOrWhiteSpace(name)
            ? name
            : FormatFallbackInstanceName("MEmu", instanceKey);
    }

    internal static string? ResolveMEmuNameFromConfigFiles(
        string instanceKey,
        IEnumerable<string> configPaths,
        IEnumerable<int> listeningPorts)
    {
        var configs = configPaths
            .Where(File.Exists)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(TryReadVirtualMachineConfig)
            .Where(config => config is not null)
            .Select(config => config!)
            .ToList();

        var byInstanceKey = ResolveVirtualMachineNameFromConfigs(configs, instanceKey, allowSingleFallback: false);
        if (!string.IsNullOrWhiteSpace(byInstanceKey))
        {
            return byInstanceKey;
        }

        var ports = listeningPorts.ToHashSet();
        if (ports.Count > 0)
        {
            var portMatches = configs
                .Where(config => config.HostPorts.Any(ports.Contains))
                .ToList();
            var byPort = portMatches.FirstOrDefault(config => !string.IsNullOrWhiteSpace(config.GuestDisplayName)) ??
                portMatches.FirstOrDefault();
            var portDisplayName = GetVirtualMachineDisplayName(byPort);
            if (!string.IsNullOrWhiteSpace(portDisplayName))
            {
                return portDisplayName;
            }
        }

        return ResolveVirtualMachineNameFromConfigs(configs, instanceKey, allowSingleFallback: true);
    }

    private static IEnumerable<string> EnumerateMEmuVirtualMachineConfigFiles(string? executablePath, string commandLine)
    {
        var candidates = EnumerateVirtualMachineConfigFiles(executablePath, commandLine, "*.memu");

        foreach (var directory in EnumerateMEmuInstallDirectories(executablePath, commandLine))
        {
            candidates = candidates.Concat(EnumerateNearbyConfigFiles(directory, "*.memu"));
        }

        return candidates.Distinct(StringComparer.OrdinalIgnoreCase);
    }

    private static IEnumerable<string> EnumerateMEmuInstallDirectories(string? executablePath, string commandLine)
    {
        var candidates = new List<string?>();
        candidates.Add(GetExistingDirectory(executablePath));

        var commandPath = QuotedPathRegex().Matches(commandLine)
            .Select(match => match.Groups["path"].Value)
            .FirstOrDefault(path => File.Exists(path) || Directory.Exists(path));
        candidates.Add(File.Exists(commandPath) ? Path.GetDirectoryName(commandPath) : commandPath);

        foreach (var processName in new[] { "MemuService", "MEmuConsole", "MEmu", "MEmuSVC", "MEmuHeadless" })
        {
            foreach (var process in Process.GetProcessesByName(processName))
            {
                using (process)
                {
                    candidates.Add(GetProcessExecutableDirectory(process));
                }
            }
        }

        return candidates
            .Where(directory => !string.IsNullOrWhiteSpace(directory) && Directory.Exists(directory))
            .Select(directory => directory!)
            .Distinct(StringComparer.OrdinalIgnoreCase);
    }

    private static string? GetProcessExecutableDirectory(Process process)
    {
        try
        {
            var path = process.MainModule?.FileName;
            return string.IsNullOrWhiteSpace(path) ? null : Path.GetDirectoryName(path);
        }
        catch
        {
            return null;
        }
    }

    private static string ResolveNoxName(string instanceKey, string? executablePath, string commandLine)
    {
        var managerName = ResolveNoxNameFromMultiPlayerManager(
            instanceKey,
            commandLine,
            EnumerateNoxMultiPlayerConfigPaths());
        if (!string.IsNullOrWhiteSpace(managerName))
        {
            return managerName;
        }

        return ResolveVirtualMachineName(executablePath, commandLine, "*.vbox", instanceKey) ??
            FormatFallbackInstanceName("Nox", instanceKey);
    }

    internal static string? ResolveNoxNameFromMultiPlayerManager(
        string instanceKey,
        string commandLine,
        IEnumerable<string> configPaths)
    {
        var candidateIds = GetNoxMultiPlayerInstanceIds(instanceKey, commandLine).ToList();
        if (candidateIds.Count == 0)
        {
            return null;
        }

        foreach (var configPath in configPaths.Where(File.Exists).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            try
            {
                var document = XDocument.Load(configPath);
                var match = document
                    .Descendants("Instance")
                    .FirstOrDefault(element => candidateIds.Any(candidateId =>
                        KeysEqual(element.Attribute("id")?.Value, candidateId)));
                var name = match?.Attribute("name")?.Value;
                if (!string.IsNullOrWhiteSpace(name))
                {
                    return name.Trim();
                }
            }
            catch
            {
                // Ignore malformed or locked third-party config files and continue with other signals.
            }
        }

        return null;
    }

    private static IEnumerable<string> GetNoxMultiPlayerInstanceIds(string instanceKey, string commandLine)
    {
        var rawKeys = new[]
            {
                instanceKey,
                GetCommandOptionValue(commandLine, "comment"),
                GetCommandOptionValue(commandLine, "instance", "vm-name", "name")
            }
            .Where(key => !string.IsNullOrWhiteSpace(key))
            .Select(key => NormalizeInstanceKey(key!))
            .Distinct(StringComparer.OrdinalIgnoreCase);

        foreach (var key in rawKeys)
        {
            yield return key;

            if (string.Equals(key, "nox", StringComparison.OrdinalIgnoreCase))
            {
                yield return "Nox_0";
            }
            else if (int.TryParse(key, out var index))
            {
                yield return $"Nox_{index}";
            }
        }
    }

    private static IEnumerable<string> EnumerateNoxMultiPlayerConfigPaths()
    {
        foreach (var baseDirectory in new[]
        {
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData)
        })
        {
            if (!string.IsNullOrWhiteSpace(baseDirectory))
            {
                yield return Path.Combine(baseDirectory, "MultiPlayerManager", "multiplayer.xml");
            }
        }
    }

    internal static string? ResolveVirtualMachineName(
        string? executablePath,
        string commandLine,
        string configPattern,
        string instanceKey)
    {
        var configs = EnumerateVirtualMachineConfigFiles(executablePath, commandLine, configPattern)
            .Select(TryReadVirtualMachineConfig)
            .Where(config => config is not null)
            .Select(config => config!)
            .ToList();

        return ResolveVirtualMachineNameFromConfigs(configs, instanceKey, allowSingleFallback: true);
    }

    private static string? ResolveVirtualMachineNameFromConfigs(
        IReadOnlyList<VirtualMachineConfig> configs,
        string instanceKey,
        bool allowSingleFallback)
    {
        var normalizedKey = NormalizeInstanceKey(instanceKey);
        var matched = configs.FirstOrDefault(config => IsVirtualMachineConfigMatch(config, normalizedKey));
        var matchedDisplayName = GetVirtualMachineDisplayName(matched);
        if (!string.IsNullOrWhiteSpace(matchedDisplayName))
        {
            return matchedDisplayName;
        }

        return allowSingleFallback && configs.Count == 1 ? GetVirtualMachineDisplayName(configs[0]) : null;
    }

    private static IEnumerable<string> EnumerateVirtualMachineConfigFiles(
        string? executablePath,
        string commandLine,
        string configPattern)
    {
        var candidates = QuotedConfigPathRegex().Matches(commandLine)
            .Select(match => match.Groups["path"].Value)
            .Where(path => path.EndsWith(configPattern.TrimStart('*'), StringComparison.OrdinalIgnoreCase));

        var directory = GetExistingDirectory(executablePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            candidates = candidates.Concat(EnumerateNearbyConfigFiles(directory, configPattern));
        }

        return candidates
            .Where(path => !string.IsNullOrWhiteSpace(path) && File.Exists(path))
            .Distinct(StringComparer.OrdinalIgnoreCase);
    }

    private static IEnumerable<string> EnumerateNearbyConfigFiles(string startDirectory, string configPattern)
    {
        var directory = startDirectory;
        for (var depth = 0; depth < 4 && !string.IsNullOrWhiteSpace(directory); depth++)
        {
            IEnumerable<string> files;
            try
            {
                files = Directory.EnumerateFiles(directory, configPattern, SearchOption.AllDirectories).ToList();
            }
            catch
            {
                files = [];
            }

            foreach (var file in files)
            {
                yield return file;
            }

            var parent = Directory.GetParent(directory);
            if (parent is null || string.Equals(parent.FullName, Path.GetPathRoot(directory), StringComparison.OrdinalIgnoreCase))
            {
                yield break;
            }

            directory = parent.FullName;
        }
    }

    private static VirtualMachineConfig? TryReadVirtualMachineConfig(string path)
    {
        try
        {
            var document = XDocument.Load(path);
            var machine = document.Descendants().FirstOrDefault(element => element.Name.LocalName == "Machine");
            var name = machine?.Attribute("name")?.Value;
            var uuid = machine?.Attribute("uuid")?.Value;
            var guestDisplayName = document
                .Descendants()
                .Where(element => element.Name.LocalName == "GuestProperty")
                .Where(element => IsVirtualMachineDisplayNameProperty(element.Attribute("name")?.Value))
                .Select(element => element.Attribute("value")?.Value)
                .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
            var hostPorts = document
                .Descendants()
                .Attributes("hostport")
                .Select(attribute => int.TryParse(attribute.Value, out var port) ? port : 0)
                .Where(port => port > 0)
                .Distinct()
                .ToList();

            return new VirtualMachineConfig(path, name, NormalizeNullableInstanceKey(uuid), guestDisplayName, hostPorts);
        }
        catch
        {
            return null;
        }
    }

    private static bool IsVirtualMachineConfigMatch(VirtualMachineConfig config, string instanceKey)
    {
        var parentDirectoryName = Path.GetFileName(Path.GetDirectoryName(config.Path));
        var fileName = Path.GetFileNameWithoutExtension(config.Path);

        return KeysEqual(config.MachineUuid, instanceKey) ||
            KeysEqual(config.MachineName, instanceKey) ||
            KeysEqual(config.GuestDisplayName, instanceKey) ||
            KeysEqual(parentDirectoryName, instanceKey) ||
            KeysEqual(fileName, instanceKey);
    }

    private static string? GetVirtualMachineDisplayName(VirtualMachineConfig? config) =>
        config is null ? null : FirstNonEmpty(config.GuestDisplayName, config.MachineName);

    private static bool IsVirtualMachineDisplayNameProperty(string? name)
    {
        return string.Equals(name, "name_tag", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(name, "playerName", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(name, "vm_name", StringComparison.OrdinalIgnoreCase);
    }

    private static string? TryReadLDPlayerName(string path)
    {
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            if (document.RootElement.TryGetProperty("statusSettings.playerName", out var playerName) &&
                playerName.ValueKind == JsonValueKind.String)
            {
                var value = playerName.GetString();
                return string.IsNullOrWhiteSpace(value) ? null : value;
            }
        }
        catch
        {
            return null;
        }

        return null;
    }

    private static IEnumerable<string> EnumerateLDPlayerConfigDirectories(string? executablePath, string commandLine)
    {
        var candidates = new List<string>();

        var executableDirectory = GetExistingDirectory(executablePath);
        if (!string.IsNullOrWhiteSpace(executableDirectory))
        {
            candidates.AddRange(EnumerateLDPlayerConfigDirectoriesNear(executableDirectory));
        }

        foreach (var directory in GetLDPlayerRegistryDirectories())
        {
            candidates.AddRange(EnumerateLDPlayerConfigDirectoriesNear(directory));
            candidates.Add(Path.Combine(directory, "config"));
        }

        foreach (var pathConfig in EnumerateLDPlayerPathConfigFiles())
        {
            foreach (var directory in ReadLDPlayerPathConfig(pathConfig))
            {
                candidates.AddRange(EnumerateLDPlayerConfigDirectoriesNear(directory));
            }
        }

        var commandPath = QuotedPathRegex().Matches(commandLine)
            .Select(match => match.Groups["path"].Value)
            .FirstOrDefault(path => File.Exists(path) || Directory.Exists(path));
        var commandDirectory = File.Exists(commandPath) ? Path.GetDirectoryName(commandPath) : commandPath;
        if (!string.IsNullOrWhiteSpace(commandDirectory))
        {
            candidates.AddRange(EnumerateLDPlayerConfigDirectoriesNear(commandDirectory));
        }

        return candidates
            .Where(path => !string.IsNullOrWhiteSpace(path) && Directory.Exists(path))
            .Distinct(StringComparer.OrdinalIgnoreCase);
    }

    private static IEnumerable<string> EnumerateLDPlayerConfigDirectoriesNear(string startDirectory)
    {
        var directory = startDirectory.TrimEnd('\\');
        for (var depth = 0; depth < 4 && !string.IsNullOrWhiteSpace(directory); depth++)
        {
            yield return Path.Combine(directory, "vms", "config");

            if (string.Equals(Path.GetFileName(directory), "vms", StringComparison.OrdinalIgnoreCase))
            {
                yield return Path.Combine(directory, "config");
            }

            var pathConfig = Path.Combine(directory, "pathconfig.ini");
            if (File.Exists(pathConfig))
            {
                foreach (var ldPlayerDirectory in ReadLDPlayerPathConfig(pathConfig))
                {
                    yield return Path.Combine(ldPlayerDirectory, "vms", "config");
                }
            }

            var siblingPathConfig = Path.Combine(directory, "ldmutiplayer", "pathconfig.ini");
            if (File.Exists(siblingPathConfig))
            {
                foreach (var ldPlayerDirectory in ReadLDPlayerPathConfig(siblingPathConfig))
                {
                    yield return Path.Combine(ldPlayerDirectory, "vms", "config");
                }
            }

            var parent = Directory.GetParent(directory);
            if (parent is null || string.Equals(parent.FullName, Path.GetPathRoot(directory), StringComparison.OrdinalIgnoreCase))
            {
                yield break;
            }

            directory = parent.FullName;
        }
    }

    private static IEnumerable<string> GetLDPlayerRegistryDirectories()
    {
        const string subKey = @"SOFTWARE\XuanZhi";
        foreach (var root in new[] { Registry.LocalMachine, Registry.CurrentUser })
        {
            using var key = root.OpenSubKey(subKey);
            if (key is null)
            {
                continue;
            }

            foreach (var subKeyName in key.GetSubKeyNames())
            {
                if (!ContainsAny(subKeyName, "LDPlayer", "ldmultiplay"))
                {
                    continue;
                }

                using var emulatorKey = key.OpenSubKey(subKeyName);
                if (emulatorKey is null)
                {
                    continue;
                }

                foreach (var valueName in new[] { "DataDir", "InstallDir", "InstallPath", "Path" })
                {
                    if (emulatorKey.GetValue(valueName) is string value && !string.IsNullOrWhiteSpace(value))
                    {
                        yield return value.TrimEnd('\\');
                    }
                }
            }
        }
    }

    private static IEnumerable<string> EnumerateLDPlayerPathConfigFiles()
    {
        var candidates = GetLDPlayerRegistryDirectories()
            .SelectMany(directory => new[]
            {
                Path.Combine(directory, "pathconfig.ini"),
                Path.Combine(directory, "ldmutiplayer", "pathconfig.ini"),
                Path.Combine(Directory.GetParent(directory)?.FullName ?? directory, "ldmutiplayer", "pathconfig.ini")
            })
            .Concat(GetFixedDriveLDPlayerPathConfigFiles());

        return candidates
            .Where(path => !string.IsNullOrWhiteSpace(path) && File.Exists(path))
            .Distinct(StringComparer.OrdinalIgnoreCase);
    }

    private static IEnumerable<string> GetFixedDriveLDPlayerPathConfigFiles()
    {
        foreach (var drive in DriveInfo.GetDrives().Where(drive => drive.DriveType == DriveType.Fixed && drive.IsReady))
        {
            foreach (var relativePath in new[]
            {
                @"LDPlayer\ldmutiplayer\pathconfig.ini",
                @"emulator\LDPlayer\ldmutiplayer\pathconfig.ini",
                @"Program Files\LDPlayer\ldmutiplayer\pathconfig.ini",
                @"Program Files (x86)\LDPlayer\ldmutiplayer\pathconfig.ini"
            })
            {
                yield return Path.Combine(drive.RootDirectory.FullName, relativePath);
            }
        }
    }

    private static IEnumerable<string> ReadLDPlayerPathConfig(string pathConfigPath)
    {
        if (!File.Exists(pathConfigPath))
        {
            yield break;
        }

        foreach (var line in File.ReadLines(pathConfigPath))
        {
            var separatorIndex = line.IndexOf('=');
            if (separatorIndex < 0)
            {
                continue;
            }

            var path = line[(separatorIndex + 1)..].Trim().Trim('"').TrimEnd('\\');
            if (!string.IsNullOrWhiteSpace(path) && Directory.Exists(path))
            {
                yield return path;
            }
        }
    }

    private static IEnumerable<string> EnumerateFiles(string directory, string pattern, SearchOption searchOption)
    {
        try
        {
            return Directory.EnumerateFiles(directory, pattern, searchOption).ToList();
        }
        catch
        {
            return [];
        }
    }

    private static string? GetCommandOptionValue(string commandLine, params string[] names)
    {
        foreach (var name in names)
        {
            var pattern = $@"(?i)(?:^|\s)(?:--?{Regex.Escape(name)}|/{Regex.Escape(name)})(?:=|\s+)(?:""(?<value>[^""]+)""|(?<value>[^\s""]+))";
            var match = Regex.Match(commandLine, pattern);
            if (match.Success)
            {
                return match.Groups["value"].Value;
            }
        }

        return null;
    }

    private static string? GetPreferredInstanceKey(EmulatorKind kind, string commandLine) =>
        kind switch
        {
            EmulatorKind.BlueStacks => GetCommandOptionValue(commandLine, "instance", "comment", "startvm", "vm-name", "name"),
            EmulatorKind.LDPlayer => GetCommandOptionValue(commandLine, "comment", "instance", "vm-name", "name", "startvm"),
            EmulatorKind.MEmu => GetCommandOptionValue(commandLine, "comment", "instance", "vm-name", "name", "startvm"),
            EmulatorKind.Nox => GetCommandOptionValue(commandLine, "comment", "instance", "vm-name", "name", "startvm"),
            _ => GetCommandOptionValue(commandLine, "instance", "comment", "startvm", "vm-name", "name")
        };

    private static string? FindBlueStacksInstanceKey(string commandLine)
    {
        var match = Regex.Match(commandLine, @"(?i)(?:^|\s)--instance(?:=|\s+)(?:""(?<value>[^""]+)""|(?<value>[^\s""]+))");
        return match.Success ? match.Groups["value"].Value : null;
    }

    private static string? FindPathSegment(string value, string pattern)
    {
        var match = Regex.Match(value, pattern, RegexOptions.IgnoreCase);
        return match.Success ? match.Groups["value"].Value : null;
    }

    private static string NormalizeInstanceKey(string value) =>
        value.Trim().Trim('"', '{', '}');

    private static string? NormalizeNullableInstanceKey(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : NormalizeInstanceKey(value);

    private static bool KeysEqual(string? left, string? right) =>
        !string.IsNullOrWhiteSpace(left) &&
        !string.IsNullOrWhiteSpace(right) &&
        string.Equals(NormalizeInstanceKey(left), NormalizeInstanceKey(right), StringComparison.OrdinalIgnoreCase);

    private static string? FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));

    private static string FormatFallbackInstanceName(string emulatorName, string instanceKey) =>
        instanceKey.StartsWith(emulatorName, StringComparison.OrdinalIgnoreCase)
            ? instanceKey
            : $"{emulatorName} - {instanceKey}";

    private static string? GetExistingDirectory(string? executablePath)
    {
        if (string.IsNullOrWhiteSpace(executablePath))
        {
            return null;
        }

        var directory = Path.GetDirectoryName(executablePath);
        return string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory) ? null : directory;
    }

    private static bool ContainsAny(string value, params string[] candidates) =>
        candidates.Any(candidate => value.Contains(candidate, StringComparison.OrdinalIgnoreCase));

    private static string? GetMainWindowTitle(int processId)
    {
        try
        {
            using var process = Process.GetProcessById(processId);
            return string.IsNullOrWhiteSpace(process.MainWindowTitle) ? null : process.MainWindowTitle;
        }
        catch
        {
            return null;
        }
    }

    private static IEnumerable<int> GetTcpListeningPortsByProcessId(int processId)
    {
        var bufferSize = 0;
        var result = GetExtendedTcpTable(
            IntPtr.Zero,
            ref bufferSize,
            order: true,
            AfInet,
            TcpTableClass.TcpTableOwnerPidListener,
            reserved: 0);
        if (result is not ErrorInsufficientBuffer and not 0)
        {
            return [];
        }

        var buffer = Marshal.AllocHGlobal(bufferSize);
        try
        {
            result = GetExtendedTcpTable(
                buffer,
                ref bufferSize,
                order: true,
                AfInet,
                TcpTableClass.TcpTableOwnerPidListener,
                reserved: 0);
            if (result != 0)
            {
                return [];
            }

            var count = Marshal.ReadInt32(buffer);
            var rowPointer = IntPtr.Add(buffer, sizeof(int));
            var rowSize = Marshal.SizeOf<MibTcpRowOwnerPid>();
            var ports = new List<int>();

            for (var index = 0; index < count; index++)
            {
                var row = Marshal.PtrToStructure<MibTcpRowOwnerPid>(rowPointer);
                if (row.OwningPid == processId)
                {
                    ports.Add(row.LocalPort);
                }

                rowPointer = IntPtr.Add(rowPointer, rowSize);
            }

            return ports;
        }
        catch
        {
            return [];
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    [GeneratedRegex(@"(?<path>[A-Z]:\\[^""]+\.(?:vbox|memu))", RegexOptions.IgnoreCase)]
    private static partial Regex QuotedConfigPathRegex();

    [GeneratedRegex(@"(?<path>[A-Z]:\\[^""]+)", RegexOptions.IgnoreCase)]
    private static partial Regex QuotedPathRegex();

    [GeneratedRegex(@"^leidian(?<index>\d+)$", RegexOptions.IgnoreCase)]
    private static partial Regex LDPlayerIndexRegex();

    [GeneratedRegex(@"^bst\.instance\.(?<key>[^.]+)\.display_name=""(?<display>[^""]*)""$", RegexOptions.IgnoreCase)]
    private static partial Regex BlueStacksDisplayNameRegex();

    private sealed record VirtualMachineConfig(
        string Path,
        string? MachineName,
        string? MachineUuid,
        string? GuestDisplayName,
        IReadOnlyList<int> HostPorts);

    private const int AfInet = 2;
    private const uint ErrorInsufficientBuffer = 122;

    private enum TcpTableClass
    {
        TcpTableOwnerPidListener = 3
    }

    [StructLayout(LayoutKind.Sequential)]
    private readonly struct MibTcpRowOwnerPid
    {
        public readonly uint State;
        public readonly uint LocalAddress;
        private readonly byte _localPort1;
        private readonly byte _localPort2;
        private readonly byte _localPort3;
        private readonly byte _localPort4;
        public readonly uint RemoteAddress;
        private readonly byte _remotePort1;
        private readonly byte _remotePort2;
        private readonly byte _remotePort3;
        private readonly byte _remotePort4;
        public readonly int OwningPid;

        public int LocalPort => _localPort1 << 8 | _localPort2;
    }

    [DllImport("iphlpapi.dll", SetLastError = true)]
    private static extern uint GetExtendedTcpTable(
        IntPtr tcpTable,
        ref int tcpTableLength,
        bool order,
        int ipVersion,
        TcpTableClass tableClass,
        int reserved);
}
