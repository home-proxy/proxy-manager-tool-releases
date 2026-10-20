using System.Text.RegularExpressions;
using NetAgent.ProxyManager.Core.Models;

namespace NetAgent.ProxyManager.Core.Services;

// Groups raw chrome.exe processes into tool-owned Chrome instances. Every Chrome child process
// carries the instance's --user-data-dir on its command line, so the profile a process belongs to
// is read straight from it — no need to walk the parent/child tree.
public static partial class ChromeProfileProcessClassifier
{
    private const string NetworkServiceMarker = "network.mojom.NetworkService";

    public static IReadOnlyList<ChromeProfileRuntimeInfo> Classify(
        IEnumerable<RunningProcessInfo> processes,
        IReadOnlyCollection<string> browserExecutablePaths)
    {
        var browserPaths = browserExecutablePaths
            .Select(NormalizePath)
            .Where(path => path.Length > 0)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (browserPaths.Count == 0)
        {
            return [];
        }

        var instances = new Dictionary<string, (int? BrowserPid, int? NetworkServicePid)>(StringComparer.OrdinalIgnoreCase);
        foreach (var process in processes)
        {
            if (string.IsNullOrWhiteSpace(process.ExecutablePath) ||
                !browserPaths.Contains(NormalizePath(process.ExecutablePath)) ||
                TryGetUserDataDirectory(process.CommandLine) is not { } userDataDirectory)
            {
                continue;
            }

            instances.TryGetValue(userDataDirectory, out var instance);
            var commandLine = process.CommandLine!;
            if (!commandLine.Contains("--type=", StringComparison.OrdinalIgnoreCase))
            {
                instance.BrowserPid = process.ProcessId;
            }
            else if (commandLine.Contains(NetworkServiceMarker, StringComparison.OrdinalIgnoreCase))
            {
                instance.NetworkServicePid = process.ProcessId;
            }

            instances[userDataDirectory] = instance;
        }

        return instances
            .Where(item => item.Value.BrowserPid is not null)
            .Select(item => new ChromeProfileRuntimeInfo(item.Key, item.Value.BrowserPid!.Value, item.Value.NetworkServicePid))
            .OrderBy(item => item.UserDataDirectory, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static string? TryGetUserDataDirectory(string? commandLine)
    {
        if (string.IsNullOrWhiteSpace(commandLine))
        {
            return null;
        }

        var match = UserDataDirRegex().Match(commandLine);
        if (!match.Success)
        {
            return null;
        }

        var value = match.Groups["quoted"].Success ? match.Groups["quoted"].Value : match.Groups["plain"].Value;
        var normalized = NormalizePath(value);
        return normalized.Length == 0 ? null : normalized;
    }

    public static bool IsSameDirectory(string? left, string? right) =>
        !string.IsNullOrWhiteSpace(left) &&
        !string.IsNullOrWhiteSpace(right) &&
        string.Equals(NormalizePath(left), NormalizePath(right), StringComparison.OrdinalIgnoreCase);

    public static string NormalizePath(string? path)
    {
        var value = (path ?? string.Empty).Trim().Trim('"').Trim();
        if (value.Length == 0)
        {
            return string.Empty;
        }

        try
        {
            value = Path.GetFullPath(value);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return string.Empty;
        }

        return value.TrimEnd('\\', '/');
    }

    // Matches --user-data-dir="C:\a b", "--user-data-dir=C:\a b" and --user-data-dir=C:\a.
    [GeneratedRegex("""(?:"--user-data-dir=(?<quoted>[^"]*)"|--user-data-dir="(?<quoted>[^"]*)"|--user-data-dir=(?<plain>[^\s"]+))""", RegexOptions.IgnoreCase)]
    private static partial Regex UserDataDirRegex();
}
