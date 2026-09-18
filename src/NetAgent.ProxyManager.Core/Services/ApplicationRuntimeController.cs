using System.Diagnostics;
using NetAgent.ProxyManager.Core.Interfaces;
using NetAgent.ProxyManager.Core.Models;

namespace NetAgent.ProxyManager.Core.Services;

public sealed class ApplicationRuntimeController : IApplicationRuntimeController
{
    private static readonly TimeSpan GracefulStopTimeout = TimeSpan.FromSeconds(8);

    public Task<ApplicationRuntimeActionResult> StartAsync(ApplicationRule rule, CancellationToken cancellationToken)
    {
        if (rule.TargetType != ApplicationTargetType.Executable)
        {
            return Task.FromResult(UnsupportedEmulatorResult());
        }

        var executablePath = NormalizeExecutablePath(rule.ExecutableName);
        if (!IsUsableExecutablePath(executablePath) || !File.Exists(executablePath))
        {
            return Task.FromResult(new ApplicationRuntimeActionResult(false, "Không tìm thấy file ứng dụng."));
        }

        cancellationToken.ThrowIfCancellationRequested();
        Process.Start(new ProcessStartInfo
        {
            FileName = executablePath,
            WorkingDirectory = Path.GetDirectoryName(executablePath) ?? string.Empty,
            UseShellExecute = true
        });

        return Task.FromResult(new ApplicationRuntimeActionResult(true, "Đã mở ứng dụng.", 1));
    }

    public async Task<ApplicationRuntimeActionResult> StopAsync(ApplicationRule rule, CancellationToken cancellationToken)
    {
        if (rule.TargetType != ApplicationTargetType.Executable)
        {
            return UnsupportedEmulatorResult();
        }

        var executablePath = NormalizeExecutablePath(rule.ExecutableName);
        if (!IsUsableExecutablePath(executablePath))
        {
            return new ApplicationRuntimeActionResult(false, "Rule chưa có đường dẫn file .exe hợp lệ.");
        }

        var processes = FindProcessesByPath(executablePath);
        if (processes.Count == 0)
        {
            return new ApplicationRuntimeActionResult(true, "Ứng dụng hiện không chạy.");
        }

        var closedCount = 0;
        var forcedCount = 0;
        try
        {
            foreach (var process in processes)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (process.HasExited)
                {
                    closedCount++;
                    continue;
                }

                var closedSoftly = process.MainWindowHandle != IntPtr.Zero &&
                    process.CloseMainWindow() &&
                    await WaitForExitAsync(process, GracefulStopTimeout, cancellationToken);
                if (closedSoftly)
                {
                    closedCount++;
                    continue;
                }

                if (await ForceKillAsync(process, cancellationToken))
                {
                    forcedCount++;
                }
            }
        }
        finally
        {
            foreach (var process in processes)
            {
                process.Dispose();
            }
        }

        var stoppedCount = closedCount + forcedCount;
        return stoppedCount == processes.Count
            ? new ApplicationRuntimeActionResult(
                true,
                forcedCount > 0 ? "Đã đóng cứng ứng dụng." : "Đã tắt ứng dụng.",
                stoppedCount)
            : new ApplicationRuntimeActionResult(
                false,
                "Không thể tắt toàn bộ ứng dụng.",
                stoppedCount);
    }

    public Task<bool> IsRunningAsync(ApplicationRule rule, CancellationToken cancellationToken)
    {
        if (rule.TargetType != ApplicationTargetType.Executable)
        {
            return Task.FromResult(false);
        }

        var executablePath = NormalizeExecutablePath(rule.ExecutableName);
        if (!IsUsableExecutablePath(executablePath))
        {
            return Task.FromResult(false);
        }

        cancellationToken.ThrowIfCancellationRequested();
        var processes = FindProcessesByPath(executablePath);
        try
        {
            return Task.FromResult(processes.Count > 0);
        }
        finally
        {
            foreach (var process in processes)
            {
                process.Dispose();
            }
        }
    }

    public async Task<ApplicationRuntimeActionResult> RestartIfRunningAsync(
        ApplicationRule rule,
        CancellationToken cancellationToken)
    {
        if (rule.TargetType != ApplicationTargetType.Executable)
        {
            return UnsupportedEmulatorResult();
        }

        var executablePath = NormalizeExecutablePath(rule.ExecutableName);
        if (!IsUsableExecutablePath(executablePath))
        {
            return new ApplicationRuntimeActionResult(false, "Rule chưa có đường dẫn file .exe hợp lệ.");
        }

        var wasRunning = await IsRunningAsync(rule, cancellationToken);
        if (!wasRunning)
        {
            return new ApplicationRuntimeActionResult(true, "Ứng dụng chưa chạy nên không cần mở lại.");
        }

        var stop = await StopAsync(rule, cancellationToken);
        if (!stop.Success && stop.AffectedProcessCount <= 0)
        {
            return stop;
        }

        await Task.Delay(500, cancellationToken);
        var start = await StartAsync(rule, cancellationToken);
        if (stop.Success)
        {
            return start;
        }

        return start.Success
            ? new ApplicationRuntimeActionResult(
                false,
                $"{stop.Message} Đã mở lại ứng dụng sau khi đóng được một phần.",
                stop.AffectedProcessCount)
            : start;
    }

    private static async Task<bool> WaitForExitAsync(
        Process process,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        try
        {
            var waitTask = process.WaitForExitAsync(cancellationToken);
            var completed = await Task.WhenAny(waitTask, Task.Delay(timeout, cancellationToken));
            return completed == waitTask && process.HasExited;
        }
        catch (InvalidOperationException)
        {
            return true;
        }
    }

    private static async Task<bool> ForceKillAsync(Process process, CancellationToken cancellationToken)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }

            return await WaitForExitAsync(process, GracefulStopTimeout, cancellationToken);
        }
        catch
        {
            return process.HasExited;
        }
    }

    private static List<Process> FindProcessesByPath(string executablePath)
    {
        var fileName = Path.GetFileNameWithoutExtension(executablePath);
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return [];
        }

        var processes = new List<Process>();
        foreach (var process in Process.GetProcessesByName(fileName))
        {
            try
            {
                var path = process.MainModule?.FileName;
                if (string.Equals(path, executablePath, StringComparison.OrdinalIgnoreCase))
                {
                    processes.Add(process);
                }
                else
                {
                    process.Dispose();
                }
            }
            catch
            {
                process.Dispose();
            }
        }

        return processes;
    }

    private static ApplicationRuntimeActionResult UnsupportedEmulatorResult() =>
        new(false, "Chưa hỗ trợ tắt/mở emulator trong phiên bản này.");

    private static string NormalizeExecutablePath(string? path) =>
        (path ?? string.Empty).Trim().Trim('"');

    private static bool IsUsableExecutablePath(string? path) =>
        !string.IsNullOrWhiteSpace(path) &&
        string.Equals(Path.GetExtension(path), ".exe", StringComparison.OrdinalIgnoreCase);
}
