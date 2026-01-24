using Api.BoundedContexts.UserLibrary.Domain.Entities;
using FluentAssertions;
using Xunit;

namespace Api.Tests.UserLibrary.Domain.Entities;

public sealed class GameSessionTests
{
    private readonly Guid _userLibraryEntryId = Guid.NewGuid();

    #region Factory Method Tests

    [Fact]
    public void Create_WithValidData_CreatesSession()
    {
        // Arrange
        var playedAt = DateTime.UtcNow.AddDays(-1);
        const int durationMinutes = 90;
        const bool didWin = true;
        const string players = "Alice, Bob, Charlie";
        const string notes = "Great game!";

        // Act
        var session = GameSession.Create(
            _userLibraryEntryId,
            playedAt,
            durationMinutes,
            didWin,
            players,
            notes);

        // Assert
        session.Should().NotBeNull();
        session.Id.Should().NotBe(Guid.Empty);
        session.UserLibraryEntryId.Should().Be(_userLibraryEntryId);
        session.PlayedAt.Should().BeCloseTo(playedAt, TimeSpan.FromSeconds(1));
        session.DurationMinutes.Should().Be(durationMinutes);
        session.DidWin.Should().Be(didWin);
        session.Players.Should().Be(players);
        session.Notes.Should().Be(notes);
        session.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
        session.UpdatedAt.Should().BeNull();
    }

    [Fact]
    public void Create_WithNonCompetitiveGame_SetsDidWinToNull()
    {
        // Arrange
        var playedAt = DateTime.UtcNow.AddDays(-1);

        // Act
        var session = GameSession.Create(_userLibraryEntryId, playedAt, 60);

        // Assert
        session.DidWin.Should().BeNull();
    }

    [Fact]
    public void Create_WithEmptyUserLibraryEntryId_ThrowsArgumentException()
    {
        // Arrange
        var playedAt = DateTime.UtcNow.AddDays(-1);

        // Act
        var act = () => GameSession.Create(Guid.Empty, playedAt, 60);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*UserLibraryEntryId cannot be empty*");
    }

    [Fact]
    public void Create_WithZeroDuration_ThrowsArgumentException()
    {
        // Arrange
        var playedAt = DateTime.UtcNow.AddDays(-1);

        // Act
        var act = () => GameSession.Create(_userLibraryEntryId, playedAt, durationMinutes: 0);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*Duration must be positive*");
    }

    [Fact]
    public void Create_WithNegativeDuration_ThrowsArgumentException()
    {
        // Arrange
        var playedAt = DateTime.UtcNow.AddDays(-1);

        // Act
        var act = () => GameSession.Create(_userLibraryEntryId, playedAt, durationMinutes: -30);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*Duration must be positive*");
    }

    [Fact]
    public void Create_WithFuturePlayedAt_ThrowsArgumentException()
    {
        // Arrange
        var futureDate = DateTime.UtcNow.AddDays(1);

        // Act
        var act = () => GameSession.Create(_userLibraryEntryId, futureDate, 60);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*PlayedAt cannot be in the future*");
    }

    [Fact]
    public void Create_TrimsPlayersAndNotes()
    {
        // Arrange
        var playedAt = DateTime.UtcNow.AddDays(-1);

        // Act
        var session = GameSession.Create(
            _userLibraryEntryId,
            playedAt,
            60,
            players: "  Alice, Bob  ",
            notes: "  Great game!  ");

        // Assert
        session.Players.Should().Be("Alice, Bob");
        session.Notes.Should().Be("Great game!");
    }

    #endregion

    #region Update Methods Tests

    [Fact]
    public void UpdateNotes_UpdatesNotesAndTimestamp()
    {
        // Arrange
        var session = GameSession.Create(_userLibraryEntryId, DateTime.UtcNow.AddDays(-1), 60);
        var originalUpdatedAt = session.UpdatedAt;

        // Act
        session.UpdateNotes("Updated notes");

        // Assert
        session.Notes.Should().Be("Updated notes");
        session.UpdatedAt.Should().NotBe(originalUpdatedAt);
        session.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void UpdateNotes_TrimsWhitespace()
    {
        // Arrange
        var session = GameSession.Create(_userLibraryEntryId, DateTime.UtcNow.AddDays(-1), 60);

        // Act
        session.UpdateNotes("  Trimmed notes  ");

        // Assert
        session.Notes.Should().Be("Trimmed notes");
    }

    [Fact]
    public void UpdatePlayers_UpdatesPlayersAndTimestamp()
    {
        // Arrange
        var session = GameSession.Create(_userLibraryEntryId, DateTime.UtcNow.AddDays(-1), 60);

        // Act
        session.UpdatePlayers("Alice, Bob, Charlie");

        // Assert
        session.Players.Should().Be("Alice, Bob, Charlie");
        session.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void UpdateOutcome_UpdatesDidWinAndTimestamp()
    {
        // Arrange
        var session = GameSession.Create(_userLibraryEntryId, DateTime.UtcNow.AddDays(-1), 60);

        // Act
        session.UpdateOutcome(true);

        // Assert
        session.DidWin.Should().BeTrue();
        session.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void CorrectDuration_WithValidDuration_UpdatesDuration()
    {
        // Arrange
        var session = GameSession.Create(_userLibraryEntryId, DateTime.UtcNow.AddDays(-1), 60);

        // Act
        session.CorrectDuration(90);

        // Assert
        session.DurationMinutes.Should().Be(90);
        session.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void CorrectDuration_WithZeroDuration_ThrowsArgumentException()
    {
        // Arrange
        var session = GameSession.Create(_userLibraryEntryId, DateTime.UtcNow.AddDays(-1), 60);

        // Act
        var act = () => session.CorrectDuration(0);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*Duration must be positive*");
    }

    [Fact]
    public void CorrectDuration_WithNegativeDuration_ThrowsArgumentException()
    {
        // Arrange
        var session = GameSession.Create(_userLibraryEntryId, DateTime.UtcNow.AddDays(-1), 60);

        // Act
        var act = () => session.CorrectDuration(-30);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*Duration must be positive*");
    }

    #endregion

    #region Helper Methods Tests

    [Fact]
    public void IsCompetitive_WithDidWinNull_ReturnsFalse()
    {
        // Arrange
        var session = GameSession.Create(_userLibraryEntryId, DateTime.UtcNow.AddDays(-1), 60, didWin: null);

        // Act
        var isCompetitive = session.IsCompetitive();

        // Assert
        isCompetitive.Should().BeFalse();
    }

    [Fact]
    public void IsCompetitive_WithDidWinTrue_ReturnsTrue()
    {
        // Arrange
        var session = GameSession.Create(_userLibraryEntryId, DateTime.UtcNow.AddDays(-1), 60, didWin: true);

        // Act
        var isCompetitive = session.IsCompetitive();

        // Assert
        isCompetitive.Should().BeTrue();
    }

    [Fact]
    public void IsCompetitive_WithDidWinFalse_ReturnsTrue()
    {
        // Arrange
        var session = GameSession.Create(_userLibraryEntryId, DateTime.UtcNow.AddDays(-1), 60, didWin: false);

        // Act
        var isCompetitive = session.IsCompetitive();

        // Assert
        isCompetitive.Should().BeTrue("competitive session even if user lost");
    }

    [Fact]
    public void GetDurationFormatted_WithHoursAndMinutes_ReturnsFormattedString()
    {
        // Arrange
        var session = GameSession.Create(_userLibraryEntryId, DateTime.UtcNow.AddDays(-1), durationMinutes: 150); // 2h 30m

        // Act
        var formatted = session.GetDurationFormatted();

        // Assert
        formatted.Should().Be("2h 30m");
    }

    [Fact]
    public void GetDurationFormatted_WithExactHours_ReturnsFormattedString()
    {
        // Arrange
        var session = GameSession.Create(_userLibraryEntryId, DateTime.UtcNow.AddDays(-1), durationMinutes: 120); // 2h

        // Act
        var formatted = session.GetDurationFormatted();

        // Assert
        formatted.Should().Be("2h");
    }

    [Fact]
    public void GetDurationFormatted_WithMinutesOnly_ReturnsFormattedString()
    {
        // Arrange
        var session = GameSession.Create(_userLibraryEntryId, DateTime.UtcNow.AddDays(-1), durationMinutes: 45);

        // Act
        var formatted = session.GetDurationFormatted();

        // Assert
        formatted.Should().Be("45m");
    }

    [Fact]
    public void GetPlayersList_WithCommaSeparatedPlayers_ReturnsList()
    {
        // Arrange
        var session = GameSession.Create(
            _userLibraryEntryId,
            DateTime.UtcNow.AddDays(-1),
            60,
            players: "Alice, Bob, Charlie");

        // Act
        var playersList = session.GetPlayersList();

        // Assert
        playersList.Should().HaveCount(3);
        playersList.Should().Contain("Alice");
        playersList.Should().Contain("Bob");
        playersList.Should().Contain("Charlie");
    }

    [Fact]
    public void GetPlayersList_WithExtraSpaces_TrimsPlayers()
    {
        // Arrange
        var session = GameSession.Create(
            _userLibraryEntryId,
            DateTime.UtcNow.AddDays(-1),
            60,
            players: "  Alice  ,  Bob  ,  Charlie  ");

        // Act
        var playersList = session.GetPlayersList();

        // Assert
        playersList.Should().HaveCount(3);
        playersList.Should().Contain("Alice");
        playersList.Should().Contain("Bob");
        playersList.Should().Contain("Charlie");
    }

    [Fact]
    public void GetPlayersList_WithNullPlayers_ReturnsEmptyList()
    {
        // Arrange
        var session = GameSession.Create(_userLibraryEntryId, DateTime.UtcNow.AddDays(-1), 60, players: null);

        // Act
        var playersList = session.GetPlayersList();

        // Assert
        playersList.Should().BeEmpty();
    }

    [Fact]
    public void GetPlayersList_WithWhitespacePlayers_ReturnsEmptyList()
    {
        // Arrange
        var session = GameSession.Create(_userLibraryEntryId, DateTime.UtcNow.AddDays(-1), 60, players: "   ");

        // Act
        var playersList = session.GetPlayersList();

        // Assert
        playersList.Should().BeEmpty();
    }

    #endregion
}
