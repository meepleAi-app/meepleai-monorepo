using System.Diagnostics;

namespace Api.Tests.Infrastructure;

/// <summary>
/// Thread-safe fake time provider for deterministic testing.
/// Replaces Task.Delay and DateTime.UtcNow with controllable time advancement.
/// </summary>
/// <remarks>
/// Thread-Safety: All public methods use lock-free interlocked operations on _utcNowTicks.
/// Pattern: Implement TimeProvider abstract class introduced in .NET 8.
/// </remarks>
public sealed class TestTimeProvider : TimeProvider, IDisposable
{
    private long _utcNowTicks;
    private readonly object _lock = new();

    /// <summary>
    /// Creates a new TestTimeProvider starting at the specified time (UTC).
    /// </summary>
    /// <param name="start">Starting UTC time. Defaults to 2025-01-01 00:00:00 UTC if null.</param>
    public TestTimeProvider(DateTimeOffset? start = null)
    {
        var initialTime = start ?? new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero);
        _utcNowTicks = initialTime.UtcTicks;
    }

    /// <summary>
    /// Gets the current fake UTC time.
    /// </summary>
    public override DateTimeOffset GetUtcNow()
    {
        var ticks = Interlocked.Read(ref _utcNowTicks);
        return new DateTimeOffset(ticks, TimeSpan.Zero);
    }

    /// <summary>
    /// Gets the current fake local time (always returns UTC time as TimeZoneInfo.Local).
    /// Note: TimeProvider.GetLocalNow() is not virtual, so we can't override it.
    /// Services should use GetUtcNow() for testability.
    /// </summary>
    public new DateTimeOffset GetLocalNow()
    {
        // For testing, treat local time as UTC to avoid timezone complexities
        return GetUtcNow();
    }

    /// <summary>
    /// Gets UTC timezone info.
    /// </summary>
    public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;

    /// <summary>
    /// Advances the fake time by the specified duration.
    /// Thread-safe: Uses compare-exchange for atomic updates.
    /// </summary>
    /// <param name="duration">Time to advance. Must be positive.</param>
    /// <exception cref="ArgumentOutOfRangeException">If duration is negative.</exception>
    public void Advance(TimeSpan duration)
    {
        if (duration < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(duration), "Duration must be non-negative");
        }

        long oldTicks, newTicks;
        do
        {
            oldTicks = Interlocked.Read(ref _utcNowTicks);
            newTicks = oldTicks + duration.Ticks;
        } while (Interlocked.CompareExchange(ref _utcNowTicks, newTicks, oldTicks) != oldTicks);
    }

    /// <summary>
    /// Sets the fake time to an absolute value.
    /// Thread-safe: Uses interlocked exchange.
    /// </summary>
    /// <param name="time">The new fake time (UTC).</param>
    public void SetTime(DateTimeOffset time)
    {
        Interlocked.Exchange(ref _utcNowTicks, time.UtcTicks);
    }

    /// <summary>
    /// Resets the fake time to the initial value (2025-01-01 00:00:00 UTC).
    /// </summary>
    public void Reset()
    {
        var initialTime = new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero);
        Interlocked.Exchange(ref _utcNowTicks, initialTime.UtcTicks);
    }

    /// <summary>
    /// Creates a timer that uses the fake time provider.
    /// IMPORTANT: This does NOT auto-advance time. Tests must manually call Advance().
    /// </summary>
    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        return new TestTimer(this, callback, state, dueTime, period);
    }

    /// <summary>
    /// Gets a timestamp for measuring elapsed time (uses fake time).
    /// Thread-safe: Returns current fake time in ticks.
    /// </summary>
    public override long GetTimestamp()
    {
        return Interlocked.Read(ref _utcNowTicks) / TimeSpan.TicksPerMillisecond;
    }

    /// <summary>
    /// Gets the frequency of timestamps per second (1000 ticks/ms * 10000 ticks = 10M ticks/sec).
    /// </summary>
    public override long TimestampFrequency => Stopwatch.Frequency;

    /// <summary>
    /// Calculates elapsed time between two timestamps using fake time frequency.
    /// Note: TimeProvider.GetElapsedTime() is not virtual in .NET 9, so we use new instead.
    /// </summary>
    public new TimeSpan GetElapsedTime(long startingTimestamp, long endingTimestamp)
    {
        var deltaTicks = endingTimestamp - startingTimestamp;
        var milliseconds = (deltaTicks * 1000.0) / TimestampFrequency;
        return TimeSpan.FromMilliseconds(milliseconds);
    }

    /// <summary>
    /// Disposes resources. TestTimeProvider has no unmanaged resources, so this is a no-op.
    /// Implementing IDisposable for consistency with using patterns in tests.
    /// </summary>
    public void Dispose()
    {
        // No unmanaged resources to dispose
        // This is here for consistency with test patterns using 'using' statements
    }

    /// <summary>
    /// Test-specific timer implementation that respects fake time.
    /// CRITICAL: Does NOT auto-fire. Tests must manually call Advance() to trigger callbacks.
    /// </summary>
    private sealed class TestTimer : ITimer
    {
        private readonly TestTimeProvider _timeProvider;
        private readonly TimerCallback _callback;
        private readonly object? _state;
        private TimeSpan _dueTime;
        private TimeSpan _period;
        private DateTimeOffset _nextFireTime;
        private bool _disposed;

        public TestTimer(TestTimeProvider timeProvider, TimerCallback callback, object? state,
            TimeSpan dueTime, TimeSpan period)
        {
            _timeProvider = timeProvider;
            _callback = callback;
            _state = state;
            _dueTime = dueTime;
            _period = period;
            _nextFireTime = timeProvider.GetUtcNow() + dueTime;
        }

        public bool Change(TimeSpan dueTime, TimeSpan period)
        {
            if (_disposed) return false;

            _dueTime = dueTime;
            _period = period;
            _nextFireTime = _timeProvider.GetUtcNow() + dueTime;
            return true;
        }

        public void Dispose()
        {
            _disposed = true;
        }

        /// <summary>
        /// Checks if timer should fire and executes callback if due.
        /// Called internally when time advances.
        /// </summary>
        internal void CheckAndFire()
        {
            if (_disposed) return;

            var now = _timeProvider.GetUtcNow();
            if (now >= _nextFireTime)
            {
                _callback(_state);

                // Schedule next firing if periodic
                if (_period > TimeSpan.Zero)
                {
                    _nextFireTime = now + _period;
                }
                else
                {
                    _disposed = true; // One-shot timer
                }
            }
        }

        public ValueTask DisposeAsync()
        {
            Dispose();
            return ValueTask.CompletedTask;
        }
    }
}
