using System;
using System.Threading;

namespace Api.BoundedContexts.SessionTracking.Infrastructure.Health;

/// <summary>
/// Thread-safe singleton tracker for AutoSaveSessionJob health.
/// Stores the last-run timestamp as Unix milliseconds in an <see cref="Interlocked"/>-protected
/// field so reads and writes are atomic across threads without locks.
/// </summary>
internal sealed class AutoSaveHealthTracker : IAutoSaveHealthTracker
{
    private readonly TimeProvider _timeProvider;
    private long _lastRunUnixMs;

    public AutoSaveHealthTracker(TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);
        _timeProvider = timeProvider;
    }

    public void RecordRun()
    {
        var nowMs = _timeProvider.GetUtcNow().ToUnixTimeMilliseconds();
        Interlocked.Exchange(ref _lastRunUnixMs, nowMs);
    }

    public long? GetLastRunAgeSeconds()
    {
        var lastMs = Interlocked.Read(ref _lastRunUnixMs);
        if (lastMs == 0)
        {
            return null;
        }

        var nowMs = _timeProvider.GetUtcNow().ToUnixTimeMilliseconds();
        var deltaMs = nowMs - lastMs;
        return deltaMs / 1000;
    }
}
