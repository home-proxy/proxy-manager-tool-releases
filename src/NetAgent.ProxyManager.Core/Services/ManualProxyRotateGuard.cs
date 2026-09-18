namespace NetAgent.ProxyManager.Core.Services;

public sealed class ManualProxyRotateGuard
{
    private readonly object _gate = new();
    private readonly TimeSpan _cooldown;
    private readonly Func<DateTimeOffset> _getUtcNow;
    private bool _isRotating;
    private DateTimeOffset _nextRotateAt = DateTimeOffset.MinValue;

    public ManualProxyRotateGuard()
        : this(TimeSpan.FromSeconds(60), () => DateTimeOffset.UtcNow)
    {
    }

    public ManualProxyRotateGuard(TimeSpan cooldown, Func<DateTimeOffset> getUtcNow)
    {
        _cooldown = cooldown;
        _getUtcNow = getUtcNow;
    }

    public bool TryBegin(out TimeSpan remaining)
    {
        lock (_gate)
        {
            var now = _getUtcNow();
            if (_isRotating)
            {
                remaining = GetRemaining(now);
                return false;
            }

            if (now < _nextRotateAt)
            {
                remaining = GetRemaining(now);
                return false;
            }

            _isRotating = true;
            remaining = TimeSpan.Zero;
            return true;
        }
    }

    public void Finish(bool succeeded)
    {
        lock (_gate)
        {
            if (succeeded)
            {
                _nextRotateAt = _getUtcNow().Add(_cooldown);
            }

            _isRotating = false;
        }
    }

    private TimeSpan GetRemaining(DateTimeOffset now)
    {
        if (now < _nextRotateAt)
        {
            return _nextRotateAt - now;
        }

        return _cooldown;
    }
}
