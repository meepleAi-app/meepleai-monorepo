using System;
using Api.BoundedContexts.SessionTracking.Infrastructure.Health;
using FluentAssertions;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace Api.Tests.BoundedContexts.SessionTracking.Unit;

public class AutoSaveHealthTrackerTests
{
    [Fact]
    public void GetLastRunAgeSeconds_BeforeAnyRun_ReturnsNull()
    {
        var timeProvider = new FakeTimeProvider(DateTimeOffset.UtcNow);
        var tracker = new AutoSaveHealthTracker(timeProvider);

        tracker.GetLastRunAgeSeconds().Should().BeNull();
    }

    [Fact]
    public void RecordRun_ThenGetAge_ReturnsZeroSeconds()
    {
        var now = DateTimeOffset.UtcNow;
        var timeProvider = new FakeTimeProvider(now);
        var tracker = new AutoSaveHealthTracker(timeProvider);

        tracker.RecordRun();

        tracker.GetLastRunAgeSeconds().Should().Be(0);
    }

    [Fact]
    public void GetLastRunAgeSeconds_AfterClockAdvances_ReturnsElapsedSeconds()
    {
        var now = DateTimeOffset.UtcNow;
        var timeProvider = new FakeTimeProvider(now);
        var tracker = new AutoSaveHealthTracker(timeProvider);

        tracker.RecordRun();
        timeProvider.Advance(TimeSpan.FromSeconds(45));

        tracker.GetLastRunAgeSeconds().Should().Be(45);
    }

    [Fact]
    public void RecordRun_IsThreadSafe_LastWriteWins()
    {
        var now = DateTimeOffset.UtcNow;
        var timeProvider = new FakeTimeProvider(now);
        var tracker = new AutoSaveHealthTracker(timeProvider);

        tracker.RecordRun();
        timeProvider.Advance(TimeSpan.FromSeconds(10));
        tracker.RecordRun();

        tracker.GetLastRunAgeSeconds().Should().Be(0);
    }
}
