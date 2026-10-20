using System.Diagnostics;

namespace NetAgent.ProxyManager.Core.Services;

// Raises RuntimeChanged as soon as a tracked Chrome NetworkService process exits (crash, restart
// after sleep, or the user closing that Chrome), so the Proxifier profile can be rebuilt with the
// new PID right away. Until then the leak-guard rule blocks that Chrome instead of letting it go
// direct. A slow periodic tick is a safety net for anything the exit events miss.
public sealed class ChromeNetworkServiceWatcher : IDisposable
{
    private static readonly TimeSpan ExitDebounce = TimeSpan.FromMilliseconds(1200);
    private static readonly TimeSpan PeriodicCheckInterval = TimeSpan.FromSeconds(30);

    private readonly object _gate = new();
    private readonly Dictionary<int, Process> _tracked = [];
    private readonly Timer _debounceTimer;
    private readonly Timer _periodicTimer;
    private bool _disposed;

    public ChromeNetworkServiceWatcher()
    {
        _debounceTimer = new Timer(_ => RaiseRuntimeChanged(), null, Timeout.Infinite, Timeout.Infinite);
        _periodicTimer = new Timer(_ => RaisePeriodicCheck(), null, PeriodicCheckInterval, PeriodicCheckInterval);
    }

    public event EventHandler? RuntimeChanged;

    public void Track(IEnumerable<int> networkServiceProcessIds)
    {
        var wanted = networkServiceProcessIds.Where(pid => pid > 0).ToHashSet();
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            foreach (var pid in _tracked.Keys.Where(pid => !wanted.Contains(pid)).ToList())
            {
                Untrack(pid);
            }

            foreach (var pid in wanted.Where(pid => !_tracked.ContainsKey(pid)))
            {
                try
                {
                    var process = Process.GetProcessById(pid);
                    process.EnableRaisingEvents = true;
                    process.Exited += HandleProcessExited;
                    _tracked[pid] = process;
                    if (process.HasExited)
                    {
                        ScheduleRecheck(TimeSpan.Zero);
                    }
                }
                catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception)
                {
                    // Already gone: make sure the caller rescans instead of trusting this PID.
                    ScheduleRecheck(TimeSpan.Zero);
                }
            }
        }
    }

    // Used while Chrome is still respawning its NetworkService (browser alive, no PID yet).
    public void ScheduleRecheck(TimeSpan delay)
    {
        lock (_gate)
        {
            if (!_disposed)
            {
                _debounceTimer.Change(delay < TimeSpan.Zero ? TimeSpan.Zero : delay, Timeout.InfiniteTimeSpan);
            }
        }
    }

    private void HandleProcessExited(object? sender, EventArgs e) => ScheduleRecheck(ExitDebounce);

    private void RaisePeriodicCheck()
    {
        lock (_gate)
        {
            if (_disposed || _tracked.Count == 0)
            {
                return;
            }
        }

        RaiseRuntimeChanged();
    }

    private void RaiseRuntimeChanged() => RuntimeChanged?.Invoke(this, EventArgs.Empty);

    private void Untrack(int pid)
    {
        if (_tracked.Remove(pid, out var process))
        {
            process.Exited -= HandleProcessExited;
            process.Dispose();
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            foreach (var pid in _tracked.Keys.ToList())
            {
                Untrack(pid);
            }
        }

        _debounceTimer.Dispose();
        _periodicTimer.Dispose();
    }
}
