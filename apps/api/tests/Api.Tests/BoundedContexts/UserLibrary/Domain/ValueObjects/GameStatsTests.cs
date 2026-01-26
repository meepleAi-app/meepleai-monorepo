using Api.BoundedContexts.UserLibrary.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.UserLibrary.Domain.ValueObjects;

/// <summary>
/// Tests for the GameStats value object.
/// Issue #3025: Backend 90% Coverage Target - Phase 8
/// </summary>
[Trait("Category", "Unit")]
public sealed class GameStatsTests
{
    #region Empty Factory Tests

    [Fact]
    public void Empty_CreatesStatsWithNoPlayHistory()
    {
        // Act
        var stats = GameStats.Empty();

        // Assert
        stats.TimesPlayed.Should().Be(0);
        stats.LastPlayed.Should().BeNull();
        stats.WinRate.Should().BeNull();
        stats.AvgDuration.Should().BeNull();
    }

    [Fact]
    public void Empty_HasPlayHistory_ReturnsFalse()
    {
        // Act
        var stats = GameStats.Empty();

        // Assert
        stats.HasPlayHistory().Should().BeFalse();
    }

    [Fact]
    public void Empty_HasCompetitiveStats_ReturnsFalse()
    {
        // Act
        var stats = GameStats.Empty();

        // Assert
        stats.HasCompetitiveStats().Should().BeFalse();
    }

    #endregion

    #region Create Factory Tests

    [Fact]
    public void Create_WithValidData_ReturnsStats()
    {
        // Arrange
        var lastPlayed = DateTime.UtcNow.AddDays(-1);

        // Act
        var stats = GameStats.Create(
            timesPlayed: 5,
            lastPlayed: lastPlayed,
            winRate: 60.0m,
            avgDuration: 90);

        // Assert
        stats.TimesPlayed.Should().Be(5);
        stats.LastPlayed.Should().Be(lastPlayed);
        stats.WinRate.Should().Be(60.0m);
        stats.AvgDuration.Should().Be(90);
    }

    [Fact]
    public void Create_WithNegativeTimesPlayed_ThrowsArgumentException()
    {
        // Act
        var action = () => GameStats.Create(
            timesPlayed: -1,
            lastPlayed: null,
            winRate: null,
            avgDuration: null);

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithMessage("*TimesPlayed cannot be negative*");
    }

    [Fact]
    public void Create_WithWinRateOver100_ThrowsArgumentException()
    {
        // Act
        var action = () => GameStats.Create(
            timesPlayed: 1,
            lastPlayed: DateTime.UtcNow,
            winRate: 101m,
            avgDuration: 60);

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithMessage("*WinRate must be between 0 and 100*");
    }

    [Fact]
    public void Create_WithNegativeWinRate_ThrowsArgumentException()
    {
        // Act
        var action = () => GameStats.Create(
            timesPlayed: 1,
            lastPlayed: DateTime.UtcNow,
            winRate: -1m,
            avgDuration: 60);

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithMessage("*WinRate must be between 0 and 100*");
    }

    [Fact]
    public void Create_WithNegativeAvgDuration_ThrowsArgumentException()
    {
        // Act
        var action = () => GameStats.Create(
            timesPlayed: 1,
            lastPlayed: DateTime.UtcNow,
            winRate: 50m,
            avgDuration: -1);

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithMessage("*AvgDuration cannot be negative*");
    }

    [Fact]
    public void Create_WithZeroPlays_AndLastPlayed_ThrowsArgumentException()
    {
        // Act
        var action = () => GameStats.Create(
            timesPlayed: 0,
            lastPlayed: DateTime.UtcNow,
            winRate: null,
            avgDuration: null);

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithMessage("*LastPlayed must be null if TimesPlayed is 0*");
    }

    [Fact]
    public void Create_WithZeroPlays_AndWinRate_ThrowsArgumentException()
    {
        // Act
        var action = () => GameStats.Create(
            timesPlayed: 0,
            lastPlayed: null,
            winRate: 50m,
            avgDuration: null);

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithMessage("*WinRate must be null if TimesPlayed is 0*");
    }

    [Fact]
    public void Create_WithZeroPlays_AndAvgDuration_ThrowsArgumentException()
    {
        // Act
        var action = () => GameStats.Create(
            timesPlayed: 0,
            lastPlayed: null,
            winRate: null,
            avgDuration: 60);

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithMessage("*AvgDuration must be null if TimesPlayed is 0*");
    }

    #endregion

    #region RecordSession Tests

    [Fact]
    public void RecordSession_FirstSession_UpdatesStats()
    {
        // Arrange
        var stats = GameStats.Empty();
        var playedAt = DateTime.UtcNow;

        // Act
        var newStats = stats.RecordSession(durationMinutes: 90, didWin: null, playedAt: playedAt);

        // Assert
        newStats.TimesPlayed.Should().Be(1);
        newStats.LastPlayed.Should().Be(playedAt);
        newStats.AvgDuration.Should().Be(90);
        newStats.WinRate.Should().BeNull(); // Non-competitive session
    }

    [Fact]
    public void RecordSession_FirstCompetitiveSession_Win_SetsWinRate100()
    {
        // Arrange
        var stats = GameStats.Empty();
        var playedAt = DateTime.UtcNow;

        // Act
        var newStats = stats.RecordSession(durationMinutes: 60, didWin: true, playedAt: playedAt);

        // Assert
        newStats.WinRate.Should().Be(100m);
    }

    [Fact]
    public void RecordSession_FirstCompetitiveSession_Loss_SetsWinRate0()
    {
        // Arrange
        var stats = GameStats.Empty();
        var playedAt = DateTime.UtcNow;

        // Act
        var newStats = stats.RecordSession(durationMinutes: 60, didWin: false, playedAt: playedAt);

        // Assert
        newStats.WinRate.Should().Be(0m);
    }

    [Fact]
    public void RecordSession_MultipleCompetitiveSessions_CalculatesWinRateCorrectly()
    {
        // Arrange
        var stats = GameStats.Empty();
        var playedAt = DateTime.UtcNow;

        // Act - Win, Loss, Win = 2/3 = 66.67%
        stats = stats.RecordSession(60, didWin: true, playedAt);
        stats = stats.RecordSession(60, didWin: false, playedAt);
        stats = stats.RecordSession(60, didWin: true, playedAt);

        // Assert
        stats.WinRate.Should().BeApproximately(66.67m, 0.1m);
    }

    [Fact]
    public void RecordSession_CalculatesAverageDuration()
    {
        // Arrange
        var stats = GameStats.Empty();
        var playedAt = DateTime.UtcNow;

        // Act - 60 + 120 = 180 / 2 = 90
        stats = stats.RecordSession(durationMinutes: 60, didWin: null, playedAt: playedAt);
        stats = stats.RecordSession(durationMinutes: 120, didWin: null, playedAt: playedAt);

        // Assert
        stats.AvgDuration.Should().Be(90);
    }

    [Fact]
    public void RecordSession_WithNegativeDuration_ThrowsArgumentException()
    {
        // Arrange
        var stats = GameStats.Empty();

        // Act
        var action = () => stats.RecordSession(durationMinutes: -1, didWin: null, playedAt: DateTime.UtcNow);

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithMessage("*Duration cannot be negative*");
    }

    [Fact]
    public void RecordSession_MixedCompetitiveAndNonCompetitive_OnlyCountsCompetitiveForWinRate()
    {
        // Arrange
        var stats = GameStats.Empty();
        var playedAt = DateTime.UtcNow;

        // Act - Non-competitive, Win, Non-competitive, Loss
        stats = stats.RecordSession(60, didWin: null, playedAt);  // Not counted for win rate
        stats = stats.RecordSession(60, didWin: true, playedAt);   // 1 win
        stats = stats.RecordSession(60, didWin: null, playedAt);  // Not counted for win rate
        stats = stats.RecordSession(60, didWin: false, playedAt);  // 1 loss

        // Assert - 1 win / 2 competitive = 50%
        stats.TimesPlayed.Should().Be(4);
        stats.WinRate.Should().Be(50m);
    }

    #endregion

    #region Formatting Tests

    [Fact]
    public void GetWinRateFormatted_WithWinRate_ReturnsPercentage()
    {
        // Arrange
        var stats = GameStats.Create(1, DateTime.UtcNow, 75m, 60);

        // Act
        var formatted = stats.GetWinRateFormatted();

        // Assert
        formatted.Should().Be("75%");
    }

    [Fact]
    public void GetWinRateFormatted_WithoutWinRate_ReturnsNA()
    {
        // Arrange
        var stats = GameStats.Empty();

        // Act
        var formatted = stats.GetWinRateFormatted();

        // Assert
        formatted.Should().Be("N/A");
    }

    [Fact]
    public void GetAvgDurationFormatted_HoursAndMinutes_FormatsCorrectly()
    {
        // Arrange
        var stats = GameStats.Create(1, DateTime.UtcNow, null, 150); // 2h 30m

        // Act
        var formatted = stats.GetAvgDurationFormatted();

        // Assert
        formatted.Should().Be("2h 30m");
    }

    [Fact]
    public void GetAvgDurationFormatted_OnlyHours_FormatsCorrectly()
    {
        // Arrange
        var stats = GameStats.Create(1, DateTime.UtcNow, null, 120); // 2h

        // Act
        var formatted = stats.GetAvgDurationFormatted();

        // Assert
        formatted.Should().Be("2h");
    }

    [Fact]
    public void GetAvgDurationFormatted_OnlyMinutes_FormatsCorrectly()
    {
        // Arrange
        var stats = GameStats.Create(1, DateTime.UtcNow, null, 45); // 45m

        // Act
        var formatted = stats.GetAvgDurationFormatted();

        // Assert
        formatted.Should().Be("45m");
    }

    [Fact]
    public void GetAvgDurationFormatted_WithoutDuration_ReturnsNA()
    {
        // Arrange
        var stats = GameStats.Empty();

        // Act
        var formatted = stats.GetAvgDurationFormatted();

        // Assert
        formatted.Should().Be("N/A");
    }

    #endregion

    #region Helper Method Tests

    [Fact]
    public void HasPlayHistory_WithPlays_ReturnsTrue()
    {
        // Arrange
        var stats = GameStats.Create(1, DateTime.UtcNow, null, 60);

        // Assert
        stats.HasPlayHistory().Should().BeTrue();
    }

    [Fact]
    public void HasCompetitiveStats_WithWinRate_ReturnsTrue()
    {
        // Arrange
        var stats = GameStats.Create(1, DateTime.UtcNow, 50m, 60);

        // Assert
        stats.HasCompetitiveStats().Should().BeTrue();
    }

    #endregion

    #region ToString Tests

    [Fact]
    public void ToString_ReturnsFormattedSummary()
    {
        // Arrange
        var stats = GameStats.Create(5, DateTime.UtcNow, 60m, 90, competitiveSessions: 3);

        // Act
        var result = stats.ToString();

        // Assert
        result.Should().Contain("Played: 5");
        result.Should().Contain("Win Rate: 60%");
        result.Should().Contain("Avg: 1h 30m");
    }

    #endregion
}
