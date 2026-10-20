using System.Diagnostics;
using System.Runtime.InteropServices;
using NetAgent.ProxyManager.Core.Interfaces;
using NetAgent.ProxyManager.Core.Models;

namespace NetAgent.ProxyManager.Core.Services;

// Lifecycle of tool-owned Chrome profiles: one ApplicationRule (TargetType = ChromeProfile) per
// profile, one --user-data-dir per rule. The data directory is the durable identity; the
// NetworkService PID stored in ApplicationRule.ProcessId is the runtime identity Proxifier routes.
// See docs/09-chrome-profiles.md.
public sealed class ChromeProfileService(
    IChromeProfileProcessScanner processScanner,
    IChromeBrowserProvisioner browserProvisioner,
    string profilesRootDirectory)
{
    private static readonly TimeSpan NetworkServiceStartTimeout = TimeSpan.FromSeconds(20);
    private static readonly TimeSpan GracefulCloseTimeout = TimeSpan.FromSeconds(6);
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(300);
    private const uint WmClose = 0x0010;

    public string ProfilesRootDirectory { get; } = profilesRootDirectory;

    public ApplicationRule CreateProfile(string name)
    {
        var trimmedName = (name ?? string.Empty).Trim();
        if (trimmedName.Length == 0)
        {
            throw new ArgumentException("Tên Chrome profile không được để trống.", nameof(name));
        }

        var rule = new ApplicationRule
        {
            TargetType = ApplicationTargetType.ChromeProfile,
            ChromeProfileName = trimmedName,
            IsEnabled = false,
            AutoAssignProxy = false
        };
        rule.ChromeUserDataDirectory = Path.Combine(ProfilesRootDirectory, rule.Id.ToString("N"));
        return rule;
    }

    public async Task<IReadOnlyList<ChromeProfileRuntimeInfo>> ScanAsync(CancellationToken cancellationToken)
    {
        var executablePaths = browserProvisioner.GetInstalledExecutablePaths();
        return executablePaths.Count == 0
            ? []
            : await processScanner.ScanAsync(executablePaths, cancellationToken);
    }

    public static ChromeProfileRuntimeInfo? FindRuntime(
        ApplicationRule rule,
        IEnumerable<ChromeProfileRuntimeInfo> runtimes) =>
        runtimes.FirstOrDefault(runtime =>
            ChromeProfileProcessClassifier.IsSameDirectory(runtime.UserDataDirectory, rule.ChromeUserDataDirectory));

    // Mirrors ApplicationRuleService.RefreshEmulatorRuntimeTargetsAsync: called right before every
    // Proxifier profile build so each open Chrome profile is routed by its current NetworkService PID.
    public async Task<IReadOnlyList<ChromeProfileRuntimeInfo>> RefreshRuntimeTargetsAsync(
        IReadOnlyList<ApplicationRule> rules,
        CancellationToken cancellationToken)
    {
        var chromeRules = rules.Where(rule => rule.TargetType == ApplicationTargetType.ChromeProfile).ToList();
        if (chromeRules.Count == 0)
        {
            return [];
        }

        var runtimes = await ScanAsync(cancellationToken);
        foreach (var rule in chromeRules)
        {
            rule.ProcessId = FindRuntime(rule, runtimes)?.NetworkServiceProcessId;
        }

        return runtimes;
    }

    public async Task<ChromeProfileRuntimeInfo> LaunchAsync(ApplicationRule rule, CancellationToken cancellationToken)
    {
        EnsureChromeProfile(rule);
        if (!browserProvisioner.IsInstalled)
        {
            throw new InvalidOperationException("Trình duyệt của tool chưa được tải về.");
        }

        var existing = FindRuntime(rule, await ScanAsync(cancellationToken));
        if (existing is null)
        {
            Directory.CreateDirectory(rule.ChromeUserDataDirectory!);
            var startInfo = new ProcessStartInfo
            {
                FileName = browserProvisioner.BrowserExecutablePath,
                WorkingDirectory = Path.GetDirectoryName(browserProvisioner.BrowserExecutablePath) ?? string.Empty,
                UseShellExecute = false
            };
            foreach (var argument in BuildLaunchArguments(rule))
            {
                startInfo.ArgumentList.Add(argument);
            }

            using var _ = Process.Start(startInfo);
        }

        var deadline = DateTime.UtcNow + NetworkServiceStartTimeout;
        while (true)
        {
            var runtime = FindRuntime(rule, await ScanAsync(cancellationToken));
            if (runtime?.NetworkServiceProcessId is > 0)
            {
                rule.ProcessId = runtime.NetworkServiceProcessId;
                return runtime;
            }

            if (DateTime.UtcNow >= deadline)
            {
                throw new TimeoutException("Chrome đã mở nhưng chưa thấy tiến trình mạng (NetworkService).");
            }

            await Task.Delay(PollInterval, cancellationToken);
        }
    }

    public static IReadOnlyList<string> BuildLaunchArguments(ApplicationRule rule) =>
    [
        $"--user-data-dir={rule.ChromeUserDataDirectory}",
        $"--window-name={rule.GetApplicationName()}",
        "--no-first-run",
        "--no-default-browser-check",
        "--hide-crash-restore-bubble",
        // UDP cannot go through an HTTP proxy and Proxifier's leak prevention blocks it anyway;
        // turning QUIC off avoids a slow fallback, and WebRTC must not use non-proxied UDP.
        "--disable-quic",
        "--force-webrtc-ip-handling-policy=disable_non_proxied_udp"
    ];

    public async Task<bool> CloseAsync(ApplicationRule rule, CancellationToken cancellationToken)
    {
        EnsureChromeProfile(rule);
        var runtime = FindRuntime(rule, await ScanAsync(cancellationToken));
        rule.ProcessId = null;
        if (runtime is null)
        {
            return false;
        }

        await CloseBrowserProcessAsync(runtime.BrowserProcessId, cancellationToken);
        return true;
    }

    public async Task<int> CloseAllAsync(CancellationToken cancellationToken)
    {
        var runtimes = await ScanAsync(cancellationToken);
        foreach (var runtime in runtimes)
        {
            await CloseBrowserProcessAsync(runtime.BrowserProcessId, cancellationToken);
        }

        return runtimes.Count;
    }

    public async Task DeleteProfileDataAsync(ApplicationRule rule, CancellationToken cancellationToken)
    {
        EnsureChromeProfile(rule);
        await CloseAsync(rule, cancellationToken);
        var directory = rule.ChromeUserDataDirectory!;
        if (!IsUnderProfilesRoot(directory))
        {
            // Never recursively delete a folder the tool did not create for this profile.
            return;
        }

        for (var attempt = 0; attempt < 10 && Directory.Exists(directory); attempt++)
        {
            try
            {
                Directory.Delete(directory, recursive: true);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Chrome releases file handles a moment after its processes exit.
                await Task.Delay(500, cancellationToken);
            }
        }
    }

    private bool IsUnderProfilesRoot(string directory)
    {
        var root = ChromeProfileProcessClassifier.NormalizePath(ProfilesRootDirectory);
        var target = ChromeProfileProcessClassifier.NormalizePath(directory);
        return root.Length > 0 &&
            target.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    }

    private static void EnsureChromeProfile(ApplicationRule rule)
    {
        if (rule.TargetType != ApplicationTargetType.ChromeProfile ||
            string.IsNullOrWhiteSpace(rule.ChromeUserDataDirectory))
        {
            throw new InvalidOperationException("Rule này không phải Chrome profile hợp lệ.");
        }
    }

    // Asks every top-level window of the browser process to close (so Chrome flushes cookies and
    // session state like a normal user close), then kills the whole tree if it does not exit in time.
    private static async Task CloseBrowserProcessAsync(int browserProcessId, CancellationToken cancellationToken)
    {
        Process process;
        try
        {
            process = Process.GetProcessById(browserProcessId);
        }
        catch (ArgumentException)
        {
            return;
        }

        using (process)
        {
            foreach (var window in GetTopLevelWindows(browserProcessId))
            {
                PostMessage(window, WmClose, IntPtr.Zero, IntPtr.Zero);
            }

            try
            {
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeout.CancelAfter(GracefulCloseTimeout);
                await process.WaitForExitAsync(timeout.Token);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    process.Kill(entireProcessTree: true);
                    await process.WaitForExitAsync(cancellationToken);
                }
                catch (InvalidOperationException)
                {
                    // Already exited.
                }
            }
        }
    }

    private static List<IntPtr> GetTopLevelWindows(int processId)
    {
        var windows = new List<IntPtr>();
        EnumWindows((window, _) =>
        {
            GetWindowThreadProcessId(window, out var ownerProcessId);
            if (ownerProcessId == processId && IsWindowVisible(window))
            {
                windows.Add(window);
            }

            return true;
        }, IntPtr.Zero);
        return windows;
    }

    private delegate bool EnumWindowsProc(IntPtr window, IntPtr parameter);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnumWindows(EnumWindowsProc callback, IntPtr parameter);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr window, out int processId);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindowVisible(IntPtr window);

    [DllImport("user32.dll", EntryPoint = "PostMessageW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool PostMessage(IntPtr window, uint message, IntPtr wParam, IntPtr lParam);
}
