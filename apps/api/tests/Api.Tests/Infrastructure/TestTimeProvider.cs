namespace Api.Tests.Infrastructure;

/// <summary>
/// Test implementation of TimeProvider with controllable time.
/// Thread-safe time manipulation for deterministic tests.
/// </summary>
/// <remarks>
/// Extends .NET 8+ TimeProvider for test scenarios.
/// Allows tests to control time progression without real delays.
/// Thread-safe for parallel test execution.
///
/// Issue: #1609 - Platform-specific timing test failures
/// Reference: https://learn.microsoft.com/en-us/dotnet/api/system.timeprovider
/// </remarks>
public sealed class TestTimeProvider : TimeProvider
{
    private readonly object _lock = new();
    private DateTime _currentTime;

    public TestTimeProvider(DateTime? startTime = null)
    {
        _currentTime = startTime ?? new DateTime(2025, 1, 1, 12, 0, 0, DateTimeKind.Utc);
    }

    /// <inheritdoc />
    public override DateTimeOffset GetUtcNow()
    {
        lock (_lock)
        {
            return new DateTimeOffset(_currentTime, TimeSpan.Zero);
        }
    }

    /// <summary>
    /// Gets current UTC time as DateTime.
    /// </summary>
    public DateTime UtcNow => GetUtcNow().UtcDateTime;

    /// <summary>
    /// Advances time by the specified amount.
    /// </summary>
    public void Advance(TimeSpan amount)
    {
        lock (_lock)
        {
            _currentTime = _currentTime.Add(amount);
        }
    }

    /// <summary>
    /// Sets time to a specific value.
    /// </summary>
    public void SetTime(DateTime time)
    {
        lock (_lock)
        {
            _currentTime = time;
        }
    }

    /// <summary>
    /// Sets time to a specific UTC value (alias for compatibility).
    /// </summary>
    public void SetUtcNow(DateTime utcTime) => SetTime(utcTime);

    /// <summary>
    /// Resets time to start value.
    /// </summary>
    public void Reset(DateTime? startTime = null)
    {
        lock (_lock)
        {
            _currentTime = startTime ?? new DateTime(2025, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        }
    }

    /// <summary>
    /// Simulates a delay by advancing time without actual waiting.
    /// </summary>
    public Task SimulateDelay(TimeSpan delay)
    {
        Advance(delay);
        return Task.CompletedTask;
    }
}
