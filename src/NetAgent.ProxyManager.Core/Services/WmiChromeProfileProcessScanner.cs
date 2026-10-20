using System.Management;
using System.Runtime.Versioning;
using NetAgent.ProxyManager.Core.Interfaces;
using NetAgent.ProxyManager.Core.Models;

namespace NetAgent.ProxyManager.Core.Services;

[SupportedOSPlatform("windows")]
public sealed class WmiChromeProfileProcessScanner : IChromeProfileProcessScanner
{
    public Task<IReadOnlyList<ChromeProfileRuntimeInfo>> ScanAsync(
        IReadOnlyCollection<string> browserExecutablePaths,
        CancellationToken cancellationToken)
    {
        if (browserExecutablePaths.Count == 0)
        {
            return Task.FromResult<IReadOnlyList<ChromeProfileRuntimeInfo>>([]);
        }

        return Task.Run(() =>
        {
            var processes = new List<RunningProcessInfo>();
            using var searcher = new ManagementObjectSearcher(
                "SELECT ProcessId,ParentProcessId,ExecutablePath,CommandLine FROM Win32_Process WHERE Name='chrome.exe'");
            foreach (ManagementObject process in searcher.Get())
            {
                cancellationToken.ThrowIfCancellationRequested();
                using (process)
                {
                    var processId = Convert.ToInt32(process["ProcessId"]);
                    processes.Add(new RunningProcessInfo(
                        processId,
                        "chrome.exe",
                        "chrome.exe",
                        MainWindowTitle: null,
                        ExecutablePath: Convert.ToString(process["ExecutablePath"]),
                        ParentProcessId: process["ParentProcessId"] is null ? null : Convert.ToInt32(process["ParentProcessId"]),
                        CommandLine: Convert.ToString(process["CommandLine"])));
                }
            }

            return ChromeProfileProcessClassifier.Classify(processes, browserExecutablePaths);
        }, cancellationToken);
    }
}
