using NetAgent.ProxyManager.Core.Models;

namespace NetAgent.ProxyManager.Core.Interfaces;

public interface IEmulatorProcessScanner
{
    Task<IReadOnlyList<EmulatorProcessCandidate>> ScanAsync(
        IReadOnlyCollection<EmulatorKind> emulatorKinds,
        CancellationToken cancellationToken);
}
