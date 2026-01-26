using Api.BoundedContexts.UserLibrary.Domain.Entities;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.UserLibrary.Domain.Entities;

/// <summary>
/// Tests for the GameSession entity.
/// Issue #3025: Backend 90% Coverage Target - Phase 28
/// </summary>
[Trait("Category", "Unit")]
public sealed class GameSessionTests
{
    #region Create Factory Tests

    [Fact]
    public void Create_WithValidData_ReturnsGameSession()
    {
        // Arrange
        var entryId = Guid.NewGuid();
        var playedAt = DateTime.UtcNow.AddHours(-2);
        var durationMinutes = 90;

        // Act
        var session = GameSession.Create(entryId, playedAt, durationMinutes);

        // Assert
        session.Should().NotBeNull();
        session.Id.Should().NotBe(Guid.Empty);
        session.UserLibraryEntryId.Should().Be(entryId);
        session.PlayedAt.Should().Be(playedAt);
        session.DurationMinutes.Should().Be(durationMinutes);
        session.DidWin.Should().BeNull();
        session.Players.Should().BeNull();
        session.Notes.Should().BeNull();
        session.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(2));
        session.UpdatedAt.Should().BeNull();
    }

    [Fact]
    public void Create_WithAllParameters_SetsAllProperties()
    {
        // Arrange
        var entryId = Guid.NewGuid();
        var playedAt = DateTime.UtcNow.AddDays(-1);
        var durationMinutes = 120;
        var didWin = true;
        var players = "Alice, Bob, Charlie";
        var notes = "Great game night!";

        // Act
        var session = GameSession.Create(
            entryId,
            playedAt,
            durationMinutes,
            didWin,
            players,
            notes);

        // Assert
        session.UserLibraryEntryId.Should().Be(entryId);
        session.PlayedAt.Should().Be(playedAt);
        session.DurationMinutes.Should().Be(durationMinutes);
        session.DidWin.Should().BeTrue();
        session.Players.Should().Be("Alice, Bob, Charlie");
        session.Notes.Should().Be("Great game night!");
    }

    [Fact]
    public void Create_WithEmptyEntryId_ThrowsArgumentException()
    {
        // Act
        var action = () => GameSession.Create(
            Guid.Empty,
            DateTime.UtcNow.AddHours(-1),
            durationMinutes: 60);

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithMessage("*UserLibraryEntryId cannot be empty*")
            .WithParameterName("userLibraryEntryId");
    }

    [Fact]
    public void Create_WithZeroDuration_ThrowsArgumentException()
    {
        // Act
        var action = () => GameSession.Create(
            Guid.NewGuid(),
            DateTime.UtcNow.AddHours(-1),
            durationMinutes: 0);

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithMessage("*Duration must be positive*")
            .WithParameterName("durationMinutes");
    }

    [Fact]
    public void Create_WithNegativeDuration_ThrowsArgumentException()
    {
        // Act
        var action = () => GameSession.Create(
            Guid.NewGuid(),
            DateTime.UtcNow.AddHours(-1),
            durationMinutes: -30);

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithMessage("*Duration must be positive*")
            .WithParameterName("durationMinutes");
    }

    [Fact]
    public void Create_WithFuturePlayedAt_ThrowsArgumentException()
    {
        // Act
        var action = () => GameSession.Create(
            Guid.NewGuid(),
            DateTime.UtcNow.AddDays(1),
            durationMinutes: 60);

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithMessage("*PlayedAt cannot be in the future*")
            .WithParameterName("playedAt");
    }

    [Fact]
    public void Create_TrimsPlayersAndNotes()
    {
        // Act
        var session = GameSession.Create(
            Guid.NewGuid(),
            DateTime.UtcNow.AddHours(-1),
            durationMinutes: 45,
            players: "  Alice, Bob  ",
            notes: "  Notes with whitespace  ");

        // Assert
        session.Players.Should().Be("Alice, Bob");
        session.Notes.Should().Be("Notes with whitespace");
    }

    [Fact]
    public void Create_WithNullPlayersAndNotes_SetsNull()
    {
        // Act
        var session = GameSession.Create(
            Guid.NewGuid(),
            DateTime.UtcNow.AddHours(-1),
            durationMinutes: 45,
            players: null,
            notes: null);

        // Assert
        session.Players.Should().BeNull();
        session.Notes.Should().BeNull();
    }

    #endregion

    #region UpdateNotes Tests

    [Fact]
    public void UpdateNotes_WithValidNotes_UpdatesNotes()
    {
        // Arrange
        var session = CreateValidSession();

        // Act
        session.UpdateNotes("Updated notes");

        // Assert
        session.Notes.Should().Be("Updated notes");
        session.UpdatedAt.Should().NotBeNull();
        session.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void UpdateNotes_TrimsWhitespace()
    {
        // Arrange
        var session = CreateValidSession();

        // Act
        session.UpdateNotes("  Notes with whitespace  ");

        // Assert
        session.Notes.Should().Be("Notes with whitespace");
    }

    [Fact]
    public void UpdateNotes_WithNull_SetsNull()
    {
        // Arrange
        var session = CreateValidSession(notes: "Initial notes");

        // Act
        session.UpdateNotes(null);

        // Assert
        session.Notes.Should().BeNull();
        session.UpdatedAt.Should().NotBeNull();
    }

    #endregion

    #region UpdatePlayers Tests

    [Fact]
    public void UpdatePlayers_WithValidPlayers_UpdatesPlayers()
    {
        // Arrange
        var session = CreateValidSession();

        // Act
        session.UpdatePlayers("Alice, Bob, Charlie");

        // Assert
        session.Players.Should().Be("Alice, Bob, Charlie");
        session.UpdatedAt.Should().NotBeNull();
    }

    [Fact]
    public void UpdatePlayers_TrimsWhitespace()
    {
        // Arrange
        var session = CreateValidSession();

        // Act
        session.UpdatePlayers("  Alice, Bob  ");

        // Assert
        session.Players.Should().Be("Alice, Bob");
    }

    [Fact]
    public void UpdatePlayers_WithNull_SetsNull()
    {
        // Arrange
        var session = CreateValidSession(players: "Alice");

        // Act
        session.UpdatePlayers(null);

        // Assert
        session.Players.Should().BeNull();
    }

    #endregion

    #region UpdateOutcome Tests

    [Fact]
    public void UpdateOutcome_WithWin_SetsDidWinTrue()
    {
        // Arrange
        var session = CreateValidSession();

        // Act
        session.UpdateOutcome(true);

        // Assert
        session.DidWin.Should().BeTrue();
        session.UpdatedAt.Should().NotBeNull();
    }

    [Fact]
    public void UpdateOutcome_WithLoss_SetsDidWinFalse()
    {
        // Arrange
        var session = CreateValidSession();

        // Act
        session.UpdateOutcome(false);

        // Assert
        session.DidWin.Should().BeFalse();
    }

    [Fact]
    public void UpdateOutcome_WithNull_SetsDidWinNull()
    {
        // Arrange
        var session = CreateValidSession(didWin: true);

        // Act
        session.UpdateOutcome(null);

        // Assert
        session.DidWin.Should().BeNull();
    }

    #endregion

    #region CorrectDuration Tests

    [Fact]
    public void CorrectDuration_WithValidDuration_UpdatesDuration()
    {
        // Arrange
        var session = CreateValidSession(durationMinutes: 60);

        // Act
        session.CorrectDuration(90);

        // Assert
        session.DurationMinutes.Should().Be(90);
        session.UpdatedAt.Should().NotBeNull();
    }

    [Fact]
    public void CorrectDuration_WithZeroDuration_ThrowsArgumentException()
    {
        // Arrange
        var session = CreateValidSession();

        // Act
        var action = () => session.CorrectDuration(0);

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithMessage("*Duration must be positive*")
            .WithParameterName("durationMinutes");
    }

    [Fact]
    public void CorrectDuration_WithNegativeDuration_ThrowsArgumentException()
    {
        // Arrange
        var session = CreateValidSession();

        // Act
        var action = () => session.CorrectDuration(-10);

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithMessage("*Duration must be positive*");
    }

    #endregion

    #region IsCompetitive Tests

    [Fact]
    public void IsCompetitive_WithDidWinTrue_ReturnsTrue()
    {
        // Arrange
        var session = CreateValidSession(didWin: true);

        // Act
        var result = session.IsCompetitive();

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void IsCompetitive_WithDidWinFalse_ReturnsTrue()
    {
        // Arrange
        var session = CreateValidSession(didWin: false);

        // Act
        var result = session.IsCompetitive();

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void IsCompetitive_WithDidWinNull_ReturnsFalse()
    {
        // Arrange
        var session = CreateValidSession(didWin: null);

        // Act
        var result = session.IsCompetitive();

        // Assert
        result.Should().BeFalse();
    }

    #endregion

    #region GetDurationFormatted Tests

    [Fact]
    public void GetDurationFormatted_WithMinutesOnly_ReturnsMinutesFormat()
    {
        // Arrange
        var session = CreateValidSession(durationMinutes: 45);

        // Act
        var result = session.GetDurationFormatted();

        // Assert
        result.Should().Be("45m");
    }

    [Fact]
    public void GetDurationFormatted_WithHoursOnly_ReturnsHoursFormat()
    {
        // Arrange
        var session = CreateValidSession(durationMinutes: 120);

        // Act
        var result = session.GetDurationFormatted();

        // Assert
        result.Should().Be("2h");
    }

    [Fact]
    public void GetDurationFormatted_WithHoursAndMinutes_ReturnsCombinedFormat()
    {
        // Arrange
        var session = CreateValidSession(durationMinutes: 150);

        // Act
        var result = session.GetDurationFormatted();

        // Assert
        result.Should().Be("2h 30m");
    }

    [Fact]
    public void GetDurationFormatted_WithOneMinute_ReturnsSingularFormat()
    {
        // Arrange
        var session = CreateValidSession(durationMinutes: 1);

        // Act
        var result = session.GetDurationFormatted();

        // Assert
        result.Should().Be("1m");
    }

    [Fact]
    public void GetDurationFormatted_WithOneHour_ReturnsOneHour()
    {
        // Arrange
        var session = CreateValidSession(durationMinutes: 60);

        // Act
        var result = session.GetDurationFormatted();

        // Assert
        result.Should().Be("1h");
    }

    #endregion

    #region GetPlayersList Tests

    [Fact]
    public void GetPlayersList_WithMultiplePlayers_ReturnsPlayerList()
    {
        // Arrange
        var session = CreateValidSession(players: "Alice, Bob, Charlie");

        // Act
        var result = session.GetPlayersList();

        // Assert
        result.Should().HaveCount(3);
        result.Should().Contain("Alice");
        result.Should().Contain("Bob");
        result.Should().Contain("Charlie");
    }

    [Fact]
    public void GetPlayersList_WithSinglePlayer_ReturnsSingleItemList()
    {
        // Arrange
        var session = CreateValidSession(players: "Alice");

        // Act
        var result = session.GetPlayersList();

        // Assert
        result.Should().HaveCount(1);
        result.Should().Contain("Alice");
    }

    [Fact]
    public void GetPlayersList_WithNullPlayers_ReturnsEmptyList()
    {
        // Arrange
        var session = CreateValidSession(players: null);

        // Act
        var result = session.GetPlayersList();

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void GetPlayersList_WithEmptyPlayers_ReturnsEmptyList()
    {
        // Arrange
        var session = CreateValidSession();
        session.UpdatePlayers("");

        // Act
        var result = session.GetPlayersList();

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void GetPlayersList_WithWhitespaceOnlyPlayers_ReturnsEmptyList()
    {
        // Arrange
        var session = CreateValidSession();
        session.UpdatePlayers("   ");

        // Act
        var result = session.GetPlayersList();

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void GetPlayersList_TrimsPlayerNames()
    {
        // Arrange
        var session = CreateValidSession(players: "  Alice  ,  Bob  ");

        // Act
        var result = session.GetPlayersList();

        // Assert
        result.Should().HaveCount(2);
        result.Should().Contain("Alice");
        result.Should().Contain("Bob");
    }

    [Fact]
    public void GetPlayersList_RemovesEmptyEntries()
    {
        // Arrange
        var session = CreateValidSession(players: "Alice,,Bob,");

        // Act
        var result = session.GetPlayersList();

        // Assert
        result.Should().HaveCount(2);
        result.Should().Contain("Alice");
        result.Should().Contain("Bob");
    }

    #endregion

    #region Helper Methods

    private static GameSession CreateValidSession(
        int durationMinutes = 60,
        bool? didWin = null,
        string? players = null,
        string? notes = null)
    {
        return GameSession.Create(
            Guid.NewGuid(),
            DateTime.UtcNow.AddHours(-1),
            durationMinutes,
            didWin,
            players,
            notes);
    }

    #endregion
}
