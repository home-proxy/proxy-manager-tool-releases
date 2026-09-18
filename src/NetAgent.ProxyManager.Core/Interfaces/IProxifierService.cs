using NetAgent.ProxyManager.Core.Models;

namespace NetAgent.ProxyManager.Core.Interfaces;

public interface IProxifierService
{
    Task<ProxifierCommandResult> LoadProfileAsync(string proxifierExePath, string profilePath, CancellationToken cancellationToken);
    Task<ProxifierCommandResult> HideWindowAsync(string proxifierExePath, CancellationToken cancellationToken);
    Task<ProxifierCommandResult> StopProcessesAsync(string proxifierExePath, CancellationToken cancellationToken);
    ProxifierWindowState GetWindowState(string proxifierExePath);
    ProxifierCommandResult ToggleWindowVisibility(string proxifierExePath);
    bool TryDetectProxifierPath(out string? path);
}
