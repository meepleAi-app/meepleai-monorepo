using Microsoft.Extensions.Time.Testing;

namespace Api.Tests.Helpers;

/// <summary>
/// Helper methods for creating and manipulating FakeTimeProvider in tests.
/// Provides fluent API for common timing scenarios.
/// </summary>
public static class TimeTestHelpers
{
    /// <summary>
    /// Creates a new FakeTimeProvider starting at a specific date/time.
    /// </summary>
    /// <param name="year">Year (e.g., 2025)</param>
    /// <param name="month">Month (1-12)</param>
    /// <param name="day">Day (1-31)</param>
    /// <param name="hour">Hour (0-23, default: 0)</param>
    /// <param name="minute">Minute (0-59, default: 0)</param>
    /// <param name="second">Second (0-59, default: 0)</param>
    /// <returns>A FakeTimeProvider initialized to the specified time (UTC)</returns>
    public static FakeTimeProvider CreateTimeProvider(
        int year = 2025,
        int month = 1,
        int day = 1,
        int hour = 0,
        int minute = 0,
        int second = 0)
    {
        var startTime = new DateTimeOffset(year, month, day, hour, minute, second, TimeSpan.Zero);
        return new FakeTimeProvider(startTime);
    }

    /// <summary>
    /// Creates a FakeTimeProvider starting at the given DateTimeOffset.
    /// </summary>
    public static FakeTimeProvider CreateTimeProvider(DateTimeOffset start)
    {
        return new FakeTimeProvider(start);
    }

    /// <summary>
    /// Creates a FakeTimeProvider starting at "now" (test execution time).
    /// Useful for tests that need realistic timestamps but want deterministic advancement.
    /// </summary>
    public static FakeTimeProvider CreateTimeProviderNow()
    {
        return new FakeTimeProvider(DateTimeOffset.UtcNow);
    }

    /// <summary>
    /// Advances time by specified number of seconds.
    /// </summary>
    public static FakeTimeProvider AdvanceSeconds(this FakeTimeProvider provider, int seconds)
    {
        provider.Advance(TimeSpan.FromSeconds(seconds));
        return provider;
    }

    /// <summary>
    /// Advances time by specified number of minutes.
    /// </summary>
    public static FakeTimeProvider AdvanceMinutes(this FakeTimeProvider provider, int minutes)
    {
        provider.Advance(TimeSpan.FromMinutes(minutes));
        return provider;
    }

    /// <summary>
    /// Advances time by specified number of hours.
    /// </summary>
    public static FakeTimeProvider AdvanceHours(this FakeTimeProvider provider, int hours)
    {
        provider.Advance(TimeSpan.FromHours(hours));
        return provider;
    }

    /// <summary>
    /// Advances time by specified number of days.
    /// </summary>
    public static FakeTimeProvider AdvanceDays(this FakeTimeProvider provider, int days)
    {
        provider.Advance(TimeSpan.FromDays(days));
        return provider;
    }

    /// <summary>
    /// Advances time by specified number of milliseconds.
    /// </summary>
    public static FakeTimeProvider AdvanceMilliseconds(this FakeTimeProvider provider, int milliseconds)
    {
        provider.Advance(TimeSpan.FromMilliseconds(milliseconds));
        return provider;
    }

    /// <summary>
    /// Advances time to the next occurrence of a specific time-of-day.
    /// Example: AdvanceToNextTime(provider, 9, 0) advances to next 9:00 AM.
    /// </summary>
    public static FakeTimeProvider AdvanceToNextTime(this FakeTimeProvider provider, int hour, int minute)
    {
        var current = provider.GetUtcNow();
        var target = new DateTimeOffset(current.Year, current.Month, current.Day, hour, minute, 0, TimeSpan.Zero);

        // If target time today has passed, advance to tomorrow
        if (target <= current)
        {
            target = target.AddDays(1);
        }

        var duration = target - current;
        provider.Advance(duration);
        return provider;
    }

    /// <summary>
    /// Sets time to a specific date and time.
    /// </summary>
    public static FakeTimeProvider SetTo(this FakeTimeProvider provider, int year, int month, int day,
        int hour = 0, int minute = 0, int second = 0)
    {
        var targetTime = new DateTimeOffset(year, month, day, hour, minute, second, TimeSpan.Zero);
        provider.SetUtcNow(targetTime);
        return provider;
    }

    /// <summary>
    /// Sets time to a specific DateTimeOffset.
    /// </summary>
    public static FakeTimeProvider SetTo(this FakeTimeProvider provider, DateTimeOffset targetTime)
    {
        provider.SetUtcNow(targetTime);
        return provider;
    }

    /// <summary>

    /// <summary>
    /// Common scenario: Session expiration (30 days default).
    /// </summary>
    public static FakeTimeProvider AdvanceToSessionExpiration(this FakeTimeProvider provider, int days = 30)
    {
        return provider.AdvanceDays(days);
    }

    /// <summary>
    /// Common scenario: Temp session expiration (5 minutes default).
    /// </summary>
    public static FakeTimeProvider AdvanceToTempSessionExpiration(this FakeTimeProvider provider, int minutes = 5)
    {
        return provider.AdvanceMinutes(minutes);
    }

    /// <summary>
    /// Common scenario: Auto-revocation check interval (1 hour default).
    /// </summary>
    public static FakeTimeProvider AdvanceToAutoRevocationCheck(this FakeTimeProvider provider, int hours = 1)
    {
        return provider.AdvanceHours(hours);
    }

    /// <summary>
    /// Common scenario: Cache warming delay (startup delay + interval).
    /// </summary>
    public static FakeTimeProvider AdvanceToCacheWarmingCycle(this FakeTimeProvider provider,
        int startupDelayMinutes = 5, int intervalHours = 6)
    {
        provider.AdvanceMinutes(startupDelayMinutes);
        provider.AdvanceHours(intervalHours);
        return provider;
    }

    /// <summary>
    /// Common scenario: Quality report generation cycle (initial delay + interval).
    /// </summary>
    public static FakeTimeProvider AdvanceToQualityReportCycle(this FakeTimeProvider provider,
        TimeSpan? initialDelay = null, TimeSpan? interval = null)
    {
        var delay = initialDelay ?? TimeSpan.FromMinutes(5);
        var reportInterval = interval ?? TimeSpan.FromHours(24);

        provider.Advance(delay);
        provider.Advance(reportInterval);
        return provider;
    }

    /// <summary>
    /// Creates a timestamp for testing elapsed time calculations.
    /// </summary>
    public static long GetTestTimestamp(this FakeTimeProvider provider)
    {
        return provider.GetTimestamp();
    }

    /// <summary>
    /// Calculates elapsed time between two test timestamps.
    /// </summary>
    public static TimeSpan CalculateElapsedTime(this FakeTimeProvider provider,
        long startTimestamp, long endTimestamp)
    {
        return provider.GetElapsedTime(startTimestamp, endTimestamp);
    }
}

/// <summary>
/// Assertion helpers for time-based testing.
/// </summary>
public static class TimeAssertions
{
    /// <summary>
    /// Asserts that a timestamp is approximately equal to expected (within tolerance).
    /// </summary>
    public static void AssertTimeNear(DateTimeOffset actual, DateTimeOffset expected,
        TimeSpan? tolerance = null)
    {
        var maxDelta = tolerance ?? TimeSpan.FromSeconds(1);
        var delta = (actual - expected).Duration();

        if (delta > maxDelta)
        {
            throw new ArgumentOutOfRangeException(
                nameof(actual),
                actual,
                $"Expected time near {expected:O}, but was {actual:O} (delta: {delta})");
        }
    }

    /// <summary>
    /// Asserts that elapsed time is within expected range.
    /// </summary>
    public static void AssertElapsedTime(TimeSpan actual, TimeSpan expected,
        TimeSpan? tolerance = null)
    {
        var maxDelta = tolerance ?? TimeSpan.FromMilliseconds(100);
        var delta = (actual - expected).Duration();

        if (delta > maxDelta)
        {
            throw new Xunit.Sdk.XunitException(
                $"Expected elapsed time near {expected}, but was {actual} (delta: {delta})");
        }
    }

    /// <summary>
    /// Asserts that a timestamp is after another timestamp.
    /// </summary>
    public static void AssertTimeAfter(DateTimeOffset actual, DateTimeOffset expectedAfter)
    {
        if (actual <= expectedAfter)
        {
            throw new Xunit.Sdk.XunitException(
                $"Expected time after {expectedAfter:O}, but was {actual:O}");
        }
    }

    /// <summary>
    /// Asserts that a timestamp is before another timestamp.
    /// </summary>
    public static void AssertTimeBefore(DateTimeOffset actual, DateTimeOffset expectedBefore)
    {
        if (actual >= expectedBefore)
        {
            throw new Xunit.Sdk.XunitException(
                $"Expected time before {expectedBefore:O}, but was {actual:O}");
        }
    }
}
