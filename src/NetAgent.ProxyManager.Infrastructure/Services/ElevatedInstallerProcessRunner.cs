using System.ComponentModel;
using System.Diagnostics;

namespace NetAgent.ProxyManager.Infrastructure.Services;

public sealed class ElevatedInstallerProcessRunner : IInstallerProcessRunner
{
    public async Task<InstallerProcessResult> RunElevatedAndWaitAsync(
        string fileName,
        string arguments,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                UseShellExecute = true,
                Verb = "runas",
                WindowStyle = ProcessWindowStyle.Hidden
            });

            if (process is null)
            {
                return new InstallerProcessResult(false, null, "Không thể khởi động ProxifierSetup.exe.");
            }

            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(timeout);

            try
            {
                await process.WaitForExitAsync(timeoutCts.Token);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                return new InstallerProcessResult(false, null, "Quá thời gian chờ ProxifierSetup.exe hoàn tất.");
            }

            return new InstallerProcessResult(true, process.ExitCode);
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == 1223)
        {
            return new InstallerProcessResult(false, null, "User đã hủy UAC khi cài Proxifier.");
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException or UnauthorizedAccessException)
        {
            return new InstallerProcessResult(false, null, $"Không thể chạy ProxifierSetup.exe: {ex.Message}");
        }
    }
}
