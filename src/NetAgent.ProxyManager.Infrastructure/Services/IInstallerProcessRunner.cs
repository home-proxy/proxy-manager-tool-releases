namespace NetAgent.ProxyManager.Infrastructure.Services;

public interface IInstallerProcessRunner
{
    Task<InstallerProcessResult> RunElevatedAndWaitAsync(
        string fileName,
        string arguments,
        TimeSpan timeout,
        CancellationToken cancellationToken);
}
