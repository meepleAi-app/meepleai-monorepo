using Api.BoundedContexts.UserLibrary.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace Api.Tests.UserLibrary.Domain.ValueObjects;

public sealed class GameStatsTests
{
    #region Factory Methods Tests

    [Fact]
    public void Empty_CreatesStatsWithZeroPlays()
    {
        // Act
        var stats = GameStats.Empty();

        // Assert
        stats.Should().NotBeNull();
        stats.TimesPlayed.Should().Be(0);
        stats.LastPlayed.Should().BeNull();
        stats.WinRate.Should().BeNull();
        stats.AvgDuration.Should().BeNull();
    }

    [Fact]
    public void Create_WithValidParameters_CreatesStats()
    {
        // Arrange
        var lastPlayed = DateTime.UtcNow.AddDays(-1);

        // Act
        var stats = GameStats.Create(
            timesPlayed: 5,
            lastPlayed: lastPlayed,
            winRate: 60m,
            avgDuration: 90);

        // Assert
        stats.TimesPlayed.Should().Be(5);
        stats.LastPlayed.Should().Be(lastPlayed);
        stats.WinRate.Should().Be(60m);
        stats.AvgDuration.Should().Be(90);
    }

    #endregion

    #region Validation Tests

    [Fact]
    public void Create_WithNegativeTimesPlayed_ThrowsArgumentException()
    {
        // Act
        Action act = () => GameStats.Create(-1, null, null, null);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*TimesPlayed*");
    }

    [Fact]
    public void Create_WithWinRateBelowZero_ThrowsArgumentException()
    {
        // Arrange
        var lastPlayed = DateTime.UtcNow;

        // Act
        Action act = () => GameStats.Create(1, lastPlayed, -1m, 60);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*WinRate*");
    }

    [Fact]
    public void Create_WithWinRateAbove100_ThrowsArgumentException()
    {
        // Arrange
        var lastPlayed = DateTime.UtcNow;

        // Act
        Action act = () => GameStats.Create(1, lastPlayed, 101m, 60);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*WinRate*");
    }

    [Fact]
    public void Create_WithNegativeDuration_ThrowsArgumentException()
    {
        // Arrange
        var lastPlayed = DateTime.UtcNow;

        // Act
        Action act = () => GameStats.Create(1, lastPlayed, 50m, -10);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*AvgDuration*");
    }

    [Fact]
    public void Create_WithZeroPlaysButStats_ThrowsArgumentException()
    {
        // Arrange
        var lastPlayed = DateTime.UtcNow;

        // Act
        Action act = () => GameStats.Create(0, lastPlayed, null, null);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*LastPlayed*");
    }

    #endregion

    #region RecordSession Tests

    [Fact]
    public void RecordSession_FirstSession_UpdatesStatsCorrectly()
    {
        // Arrange
        var stats = GameStats.Empty();
        var playedAt = DateTime.UtcNow;

        // Act
        var updated = stats.RecordSession(
            durationMinutes: 60,
            didWin: true,
            playedAt: playedAt);

        // Assert
        updated.TimesPlayed.Should().Be(1);
        updated.LastPlayed.Should().Be(playedAt);
        updated.AvgDuration.Should().Be(60);
        updated.WinRate.Should().Be(100m, "first competitive session won");
    }

    [Fact]
    public void RecordSession_SecondSession_UpdatesAverageDuration()
    {
        // Arrange
        var firstPlayed = DateTime.UtcNow.AddDays(-1);
        var stats = GameStats.Create(1, firstPlayed, null, 60);
        var secondPlayed = DateTime.UtcNow;

        // Act
        var updated = stats.RecordSession(
            durationMinutes: 90,
            didWin: null, // non-competitive
            playedAt: secondPlayed);

        // Assert
        updated.TimesPlayed.Should().Be(2);
        updated.LastPlayed.Should().Be(secondPlayed);
        updated.AvgDuration.Should().Be(75, "(60 + 90) / 2 = 75");
        updated.WinRate.Should().BeNull("no competitive sessions");
    }

    [Fact]
    public void RecordSession_MultipleCompetitiveSessions_CalculatesWinRate()
    {
        // Arrange
        var stats = GameStats.Empty();
        var playedAt = DateTime.UtcNow;

        // Act - Win first
        stats = stats.RecordSession(60, true, playedAt.AddDays(-3));
        stats = stats.RecordSession(60, false, playedAt.AddDays(-2));
        stats = stats.RecordSession(60, true, playedAt.AddDays(-1));
        stats = stats.RecordSession(60, true, playedAt);

        // Assert
        stats.TimesPlayed.Should().Be(4);
        stats.WinRate.Should().Be(75m, "3 wins out of 4 sessions = 75%");
    }

    [Fact]
    public void RecordSession_NonCompetitiveSession_DoesNotAffectWinRate()
    {
        // Arrange
        var stats = GameStats.Create(1, DateTime.UtcNow.AddDays(-1), 100m, 60);

        // Act
        var updated = stats.RecordSession(
            durationMinutes: 90,
            didWin: null, // non-competitive
            playedAt: DateTime.UtcNow);

        // Assert
        updated.WinRate.Should().Be(100m, "non-competitive sessions should not affect win rate");
    }

    [Fact]
    public void RecordSession_WithNegativeDuration_ThrowsArgumentException()
    {
        // Arrange
        var stats = GameStats.Empty();

        // Act
        Action act = () => stats.RecordSession(-10, null, DateTime.UtcNow);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*Duration*");
    }

    #endregion

    #region Helper Methods Tests

    [Fact]
    public void HasPlayHistory_WithZeroPlays_ReturnsFalse()
    {
        // Arrange
        var stats = GameStats.Empty();

        // Act & Assert
        stats.HasPlayHistory().Should().BeFalse();
    }

    [Fact]
    public void HasPlayHistory_WithPlays_ReturnsTrue()
    {
        // Arrange
        var stats = GameStats.Create(1, DateTime.UtcNow, null, 60);

        // Act & Assert
        stats.HasPlayHistory().Should().BeTrue();
    }

    [Fact]
    public void HasCompetitiveStats_WithoutWinRate_ReturnsFalse()
    {
        // Arrange
        var stats = GameStats.Create(1, DateTime.UtcNow, null, 60);

        // Act & Assert
        stats.HasCompetitiveStats().Should().BeFalse();
    }

    [Fact]
    public void HasCompetitiveStats_WithWinRate_ReturnsTrue()
    {
        // Arrange
        var stats = GameStats.Create(1, DateTime.UtcNow, 100m, 60);

        // Act & Assert
        stats.HasCompetitiveStats().Should().BeTrue();
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
    public void GetWinRateFormatted_WithWinRate_ReturnsPercentage()
    {
        // Arrange
        var stats = GameStats.Create(1, DateTime.UtcNow, 75.5m, 60);

        // Act
        var formatted = stats.GetWinRateFormatted();

        // Assert
        formatted.Should().Be("76%", "formatted as integer percentage");
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

    [Fact]
    public void GetAvgDurationFormatted_WithMinutesOnly_ReturnsMinutes()
    {
        // Arrange
        var stats = GameStats.Create(1, DateTime.UtcNow, null, 45);

        // Act
        var formatted = stats.GetAvgDurationFormatted();

        // Assert
        formatted.Should().Be("45m");
    }

    [Fact]
    public void GetAvgDurationFormatted_WithHoursAndMinutes_ReturnsFormatted()
    {
        // Arrange
        var stats = GameStats.Create(1, DateTime.UtcNow, null, 150); // 2h 30m

        // Act
        var formatted = stats.GetAvgDurationFormatted();

        // Assert
        formatted.Should().Be("2h 30m");
    }

    [Fact]
    public void GetAvgDurationFormatted_WithHoursOnly_ReturnsHoursOnly()
    {
        // Arrange
        var stats = GameStats.Create(1, DateTime.UtcNow, null, 120); // 2h exactly

        // Act
        var formatted = stats.GetAvgDurationFormatted();

        // Assert
        formatted.Should().Be("2h");
    }

    #endregion
}
