using NetAgent.ProxyManager.Core.Models;

namespace NetAgent.ProxyManager.Core.Services;

public sealed class ProfileAutoReloadCoordinator(
    Func<bool> shouldReload,
    Func<ProfileAffectingChange, CancellationToken, Task> reloadAsync)
{
    private readonly object _gate = new();
    private bool _isReloading;
    private ProfileAffectingChange? _pendingChange;

    public Task RequestReloadAsync(ProfileAffectingChange change, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(change);

        if (!shouldReload())
        {
            return Task.CompletedTask;
        }

        lock (_gate)
        {
            if (_isReloading)
            {
                _pendingChange = _pendingChange is null ? change : _pendingChange.Merge(change);
                return Task.CompletedTask;
            }

            _isReloading = true;
        }

        return RunReloadLoopAsync(change, cancellationToken);
    }

    private async Task RunReloadLoopAsync(ProfileAffectingChange initialChange, CancellationToken cancellationToken)
    {
        var change = initialChange;
        try
        {
            while (shouldReload())
            {
                await reloadAsync(change, cancellationToken);

                lock (_gate)
                {
                    if (_pendingChange is null)
                    {
                        _isReloading = false;
                        return;
                    }

                    change = _pendingChange;
                    _pendingChange = null;
                }
            }
        }
        finally
        {
            lock (_gate)
            {
                _isReloading = false;
                _pendingChange = null;
            }
        }
    }
}
