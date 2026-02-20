using Api.BoundedContexts.GameManagement.Domain.Entities.SessionSnapshot;
using Api.BoundedContexts.GameManagement.Domain.ValueObjects;
using Api.SharedKernel.Domain.Exceptions;
using Api.Tests.Constants;
using Xunit;

namespace Api.Tests.BoundedContexts.GameManagement.Domain.ValueObjects;

/// <summary>
/// Tests for SnapshotTriggerConfig value object.
/// Issue #4761: Turn Phases from TurnTemplate + Event-Triggered Snapshots.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "GameManagement")]
public class SnapshotTriggerConfigTests
{
    #region Constructor

    [Fact]
    public void CreateDefault_ReturnsConfigWithDefaultTriggers()
    {
        var config = SnapshotTriggerConfig.CreateDefault();

        Assert.Contains(SnapshotTrigger.TurnAdvanced, config.EnabledTriggers);
        Assert.Contains(SnapshotTrigger.PhaseAdvanced, config.EnabledTriggers);
        Assert.Contains(SnapshotTrigger.ManualSave, config.EnabledTriggers);
        Assert.Equal(5, config.DebounceDurationSeconds);
        Assert.Equal(12, config.MaxSnapshotsPerMinute);
    }

    [Fact]
    public void Constructor_CustomTriggers_SetsCorrectly()
    {
        var triggers = new[] { SnapshotTrigger.ScoreChanged, SnapshotTrigger.TimerExpired };
        var config = new SnapshotTriggerConfig(triggers, 10, 20);

        Assert.Equal(2, config.EnabledTriggers.Count);
        Assert.Contains(SnapshotTrigger.ScoreChanged, config.EnabledTriggers);
        Assert.Contains(SnapshotTrigger.TimerExpired, config.EnabledTriggers);
        Assert.Equal(10, config.DebounceDurationSeconds);
        Assert.Equal(20, config.MaxSnapshotsPerMinute);
    }

    [Fact]
    public void Constructor_DuplicateTriggers_DeduplicatesAutomatically()
    {
        var triggers = new[]
        {
            SnapshotTrigger.TurnAdvanced,
            SnapshotTrigger.TurnAdvanced,
            SnapshotTrigger.PhaseAdvanced
        };
        var config = new SnapshotTriggerConfig(triggers);

        Assert.Equal(2, config.EnabledTriggers.Count);
    }

    [Fact]
    public void Constructor_DebounceTooLow_ThrowsValidation()
    {
        Assert.Throws<ValidationException>(() =>
            new SnapshotTriggerConfig(debounceDurationSeconds: 0));
    }

    [Fact]
    public void Constructor_DebounceTooHigh_ThrowsValidation()
    {
        Assert.Throws<ValidationException>(() =>
            new SnapshotTriggerConfig(debounceDurationSeconds: 301));
    }

    [Fact]
    public void Constructor_MaxSnapshotsTooLow_ThrowsValidation()
    {
        Assert.Throws<ValidationException>(() =>
            new SnapshotTriggerConfig(maxSnapshotsPerMinute: 0));
    }

    [Fact]
    public void Constructor_MaxSnapshotsTooHigh_ThrowsValidation()
    {
        Assert.Throws<ValidationException>(() =>
            new SnapshotTriggerConfig(maxSnapshotsPerMinute: 61));
    }

    #endregion

    #region IsTriggerEnabled

    [Fact]
    public void IsTriggerEnabled_EnabledTrigger_ReturnsTrue()
    {
        var config = SnapshotTriggerConfig.CreateDefault();

        Assert.True(config.IsTriggerEnabled(SnapshotTrigger.TurnAdvanced));
        Assert.True(config.IsTriggerEnabled(SnapshotTrigger.PhaseAdvanced));
    }

    [Fact]
    public void IsTriggerEnabled_DisabledTrigger_ReturnsFalse()
    {
        var config = SnapshotTriggerConfig.CreateDefault();

        Assert.False(config.IsTriggerEnabled(SnapshotTrigger.ScoreChanged));
        Assert.False(config.IsTriggerEnabled(SnapshotTrigger.TimerExpired));
    }

    #endregion

    #region ShouldCreateSnapshot (Debounce)

    [Fact]
    public void ShouldCreateSnapshot_NoLastTimestamp_ReturnsTrue()
    {
        var config = new SnapshotTriggerConfig(debounceDurationSeconds: 5);
        var now = DateTime.UtcNow;

        Assert.True(config.ShouldCreateSnapshot(null, now));
    }

    [Fact]
    public void ShouldCreateSnapshot_WithinDebouncePeriod_ReturnsFalse()
    {
        var config = new SnapshotTriggerConfig(debounceDurationSeconds: 5);
        var lastSnapshot = new DateTime(2026, 2, 19, 14, 0, 0, DateTimeKind.Utc);
        var now = lastSnapshot.AddSeconds(3); // Only 3 seconds, debounce is 5

        Assert.False(config.ShouldCreateSnapshot(lastSnapshot, now));
    }

    [Fact]
    public void ShouldCreateSnapshot_AfterDebouncePeriod_ReturnsTrue()
    {
        var config = new SnapshotTriggerConfig(debounceDurationSeconds: 5);
        var lastSnapshot = new DateTime(2026, 2, 19, 14, 0, 0, DateTimeKind.Utc);
        var now = lastSnapshot.AddSeconds(6); // 6 seconds, debounce is 5

        Assert.True(config.ShouldCreateSnapshot(lastSnapshot, now));
    }

    [Fact]
    public void ShouldCreateSnapshot_ExactlyAtDebounceBoundary_ReturnsTrue()
    {
        var config = new SnapshotTriggerConfig(debounceDurationSeconds: 5);
        var lastSnapshot = new DateTime(2026, 2, 19, 14, 0, 0, DateTimeKind.Utc);
        var now = lastSnapshot.AddSeconds(5); // Exactly 5 seconds

        Assert.True(config.ShouldCreateSnapshot(lastSnapshot, now));
    }

    #endregion

    #region Equality

    [Fact]
    public void Equality_SameConfig_AreEqual()
    {
        var config1 = new SnapshotTriggerConfig(
            new[] { SnapshotTrigger.TurnAdvanced, SnapshotTrigger.PhaseAdvanced }, 5, 12);
        var config2 = new SnapshotTriggerConfig(
            new[] { SnapshotTrigger.PhaseAdvanced, SnapshotTrigger.TurnAdvanced }, 5, 12);

        Assert.Equal(config1, config2);
    }

    [Fact]
    public void Equality_DifferentConfig_AreNotEqual()
    {
        var config1 = new SnapshotTriggerConfig(debounceDurationSeconds: 5);
        var config2 = new SnapshotTriggerConfig(debounceDurationSeconds: 10);

        Assert.NotEqual(config1, config2);
    }

    #endregion
}
