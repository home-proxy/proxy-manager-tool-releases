using System.Diagnostics;
using System.Runtime.InteropServices;
using NetAgent.ProxyManager.Core.Interfaces;
using NetAgent.ProxyManager.Core.Models;

namespace NetAgent.ProxyManager.Core.Services;

public sealed class ProxifierService : IProxifierService
{
    private static readonly string[] CommonInstallPaths =
    [
        @"C:\Program Files (x86)\Proxifier\Proxifier.exe",
        @"C:\Program Files\Proxifier\Proxifier.exe"
    ];

    public async Task<ProxifierCommandResult> LoadProfileAsync(
        string proxifierExePath,
        string profilePath,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(proxifierExePath))
        {
            return new ProxifierCommandResult(false, "Không tìm thấy Proxifier.exe.");
        }

        if (!File.Exists(profilePath))
        {
            return new ProxifierCommandResult(false, "Không tìm thấy file profile .ppx.");
        }

        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = proxifierExePath,
                Arguments = BuildArguments(profilePath),
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(startInfo);
            if (process is null)
            {
                return new ProxifierCommandResult(false, "Không thể khởi chạy Proxifier.");
            }

            var waitTask = process.WaitForExitAsync(cancellationToken);
            var completed = await Task.WhenAny(waitTask, Task.Delay(TimeSpan.FromSeconds(2), cancellationToken));
            if (completed == waitTask)
            {
                return process.ExitCode == 0
                    ? new ProxifierCommandResult(true, "Đã gửi lệnh load profile vào Proxifier.", process.ExitCode)
                    : new ProxifierCommandResult(false, $"Proxifier trả về exit code {process.ExitCode}.", process.ExitCode);
            }

            return new ProxifierCommandResult(true, "Đã gửi lệnh load profile vào Proxifier.");
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception or UnauthorizedAccessException)
        {
            return new ProxifierCommandResult(false, $"Không thể load profile: {ex.Message}");
        }
    }

    public bool TryDetectProxifierPath(out string? path)
    {
        path = CommonInstallPaths.FirstOrDefault(File.Exists);
        return path is not null;
    }

    public async Task<ProxifierCommandResult> HideWindowAsync(
        string proxifierExePath,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(proxifierExePath))
        {
            return new ProxifierCommandResult(false, "Không tìm thấy Proxifier.exe.");
        }

        try
        {
            for (var attempt = 0; attempt < 15; attempt++)
            {
                var processes = FindProcesses(proxifierExePath);
                var foundWindow = false;
                foreach (var process in processes)
                {
                    using (process)
                    {
                        process.Refresh();
                        if (process.MainWindowHandle == IntPtr.Zero)
                        {
                            continue;
                        }

                        foundWindow = true;
                        if (IsWindowVisible(process.MainWindowHandle))
                        {
                            ShowWindow(process.MainWindowHandle, SwHide);
                        }
                    }
                }

                if (foundWindow)
                {
                    return new ProxifierCommandResult(true, "Đã ẩn cửa sổ proxy.");
                }

                await Task.Delay(200, cancellationToken);
            }

            return new ProxifierCommandResult(true, "Proxy đang chạy ngầm.");
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception or UnauthorizedAccessException)
        {
            return new ProxifierCommandResult(false, $"Không thể ẩn cửa sổ proxy: {ex.Message}");
        }
    }

    public async Task<ProxifierCommandResult> StopProcessesAsync(
        string proxifierExePath,
        CancellationToken cancellationToken)
    {
        try
        {
            var processes = FindProcesses(proxifierExePath);
            if (processes.Count == 0)
            {
                return new ProxifierCommandResult(true, "Proxy đã dừng.");
            }

            foreach (var process in processes)
            {
                using (process)
                {
                    if (process.HasExited)
                    {
                        continue;
                    }

                    if (process.MainWindowHandle != IntPtr.Zero)
                    {
                        process.CloseMainWindow();
                    }
                }
            }

            var deadline = DateTimeOffset.UtcNow.AddSeconds(2);
            while (DateTimeOffset.UtcNow < deadline)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (FindProcesses(proxifierExePath).Count == 0)
                {
                    return new ProxifierCommandResult(true, "Đã dừng proxy.");
                }

                await Task.Delay(150, cancellationToken);
            }

            foreach (var process in FindProcesses(proxifierExePath))
            {
                using (process)
                {
                    if (!process.HasExited)
                    {
                        process.Kill(entireProcessTree: true);
                        await process.WaitForExitAsync(cancellationToken);
                    }
                }
            }

            return new ProxifierCommandResult(true, "Đã dừng proxy.");
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception or UnauthorizedAccessException)
        {
            return new ProxifierCommandResult(false, $"Không thể dừng proxy: {ex.Message}");
        }
    }

    public static string BuildArguments(string profilePath) => $"\"{profilePath}\" silent-load";

    public ProxifierWindowState GetWindowState(string proxifierExePath)
    {
        var process = FindWindowedProcess(proxifierExePath);
        if (process is null)
        {
            return ProxifierWindowState.NotRunning;
        }

        return IsWindowVisible(process.MainWindowHandle)
            ? ProxifierWindowState.Visible
            : ProxifierWindowState.Hidden;
    }

    public ProxifierCommandResult ToggleWindowVisibility(string proxifierExePath)
    {
        if (!File.Exists(proxifierExePath))
        {
            return new ProxifierCommandResult(false, "Không tìm thấy Proxifier.exe.");
        }

        var process = FindWindowedProcess(proxifierExePath);

        if (process is null)
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = proxifierExePath,
                UseShellExecute = true
            });
            return new ProxifierCommandResult(true, "Đã mở Proxifier.");
        }

        if (IsWindowVisible(process.MainWindowHandle))
        {
            ShowWindow(process.MainWindowHandle, SwHide);
            return new ProxifierCommandResult(true, "Đã ẩn Proxifier.");
        }

        ShowWindow(process.MainWindowHandle, SwRestore);
        SetForegroundWindow(process.MainWindowHandle);
        return new ProxifierCommandResult(true, "Đã hiển thị Proxifier.");
    }

    private static Process? FindWindowedProcess(string proxifierExePath)
    {
        if (string.IsNullOrWhiteSpace(proxifierExePath))
        {
            return null;
        }

        foreach (var process in FindProcesses(proxifierExePath))
        {
            if (process.MainWindowHandle != IntPtr.Zero)
            {
                return process;
            }

            process.Dispose();
        }

        return null;
    }

    private static List<Process> FindProcesses(string proxifierExePath)
    {
        if (string.IsNullOrWhiteSpace(proxifierExePath))
        {
            return [];
        }

        var processName = Path.GetFileNameWithoutExtension(proxifierExePath);
        var expectedPath = Path.GetFullPath(proxifierExePath);
        var matches = new List<Process>();

        foreach (var process in Process.GetProcessesByName(processName))
        {
            try
            {
                var actualPath = process.MainModule?.FileName;
                if (string.IsNullOrWhiteSpace(actualPath) ||
                    string.Equals(Path.GetFullPath(actualPath), expectedPath, StringComparison.OrdinalIgnoreCase))
                {
                    matches.Add(process);
                }
                else
                {
                    process.Dispose();
                }
            }
            catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException or UnauthorizedAccessException)
            {
                matches.Add(process);
            }
        }

        return matches;
    }

    private const int SwHide = 0;
    private const int SwRestore = 9;

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);
}
