using NetAgent.ProxyManager.Core.Models;

namespace NetAgent.ProxyManager.Core.Interfaces;

public interface IApplicationRuntimeController
{
    Task<ApplicationRuntimeActionResult> StartAsync(ApplicationRule rule, CancellationToken cancellationToken);

    Task<ApplicationRuntimeActionResult> StopAsync(ApplicationRule rule, CancellationToken cancellationToken);

    Task<bool> IsRunningAsync(ApplicationRule rule, CancellationToken cancellationToken);

    Task<ApplicationRuntimeActionResult> RestartIfRunningAsync(ApplicationRule rule, CancellationToken cancellationToken);
}
