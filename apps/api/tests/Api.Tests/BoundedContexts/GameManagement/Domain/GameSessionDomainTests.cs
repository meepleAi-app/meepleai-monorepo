using Api.BoundedContexts.GameManagement.Domain.Entities;
using Api.BoundedContexts.GameManagement.Domain.ValueObjects;
using FluentAssertions;
using Xunit;
using Api.Tests.Constants;

namespace Api.Tests.BoundedContexts.GameManagement.Domain;

[Trait("Category", TestCategories.Unit)]

public class GameSessionDomainTests
{
    [Fact]
    public void GameSession_WithValidPlayers_CreatesSuccessfully()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var players = new List<SessionPlayer>
        {
            new SessionPlayer("Alice", 1, "Red"),
            new SessionPlayer("Bob", 2, "Blue")
        };

        // Act
        var session = new GameSession(sessionId, gameId, players);

        // Assert
        session.Id.Should().Be(sessionId);
        session.GameId.Should().Be(gameId);
        session.Status.Should().Be(SessionStatus.Setup);
        session.PlayerCount.Should().Be(2);
        session.Players.Count.Should().Be(2);
    }

    [Fact]
    public void GameSession_EmptyGameId_ThrowsArgumentException()
    {
        // Arrange
        var players = new List<SessionPlayer> { new SessionPlayer("Alice", 1) };

        // Act & Assert
        var act = () => new GameSession(Guid.NewGuid(), Guid.Empty, players);
        var exception = act.Should().Throw<ArgumentException>().Which;
        exception.Message.Should().ContainEquivalentOf("GameId cannot be empty");
    }

    [Fact]
    public void GameSession_NullPlayers_ThrowsArgumentNullException()
    {
        // Act & Assert
        var act = () => new GameSession(Guid.NewGuid(), Guid.NewGuid(), null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void GameSession_EmptyPlayersList_ThrowsArgumentException()
    {
        // Arrange
        var players = new List<SessionPlayer>();

        // Act & Assert
        var act = () => new GameSession(Guid.NewGuid(), Guid.NewGuid(), players);
        var exception = act.Should().Throw<ArgumentException>().Which;
        exception.Message.Should().ContainEquivalentOf("at least one player");
    }

    [Fact]
    public void GameSession_TooManyPlayers_ThrowsArgumentException()
    {
        // Arrange - Create 101 players (all with same PlayerOrder to avoid VO validation)
        var players = Enumerable.Range(1, 101)
            .Select(i => new SessionPlayer($"Player{i}", 1)) // All order=1 to bypass VO validation
            .ToList();

        // Act & Assert
        var act = () => new GameSession(Guid.NewGuid(), Guid.NewGuid(), players);
        var exception = act.Should().Throw<ArgumentException>().Which;
        exception.Message.Should().ContainEquivalentOf("cannot have more than 100 players");
    }

    [Fact]
    public void GameSession_Start_MovesFromSetupToInProgress()
    {
        // Arrange
        var session = CreateDefaultSession();

        // Act
        session.Start();

        // Assert
        session.Status.Should().Be(SessionStatus.InProgress);
    }

    [Fact]
    public void GameSession_Start_WhenAlreadyStarted_ThrowsInvalidOperationException()
    {
        // Arrange
        var session = CreateDefaultSession();
        session.Start();

        // Act & Assert
        var act = () => session.Start();
        var exception = act.Should().Throw<InvalidOperationException>().Which;
        exception.Message.Should().ContainEquivalentOf("must be in Setup");
    }

    [Fact]
    public void GameSession_Complete_SetsStatusAndTimestamp()
    {
        // Arrange
        var session = CreateDefaultSession();
        session.Start();
        var beforeComplete = DateTime.UtcNow;

        // Act
        session.Complete("Alice");

        // Assert
        var afterComplete = DateTime.UtcNow;
        session.Status.Should().Be(SessionStatus.Completed);
        session.WinnerName.Should().Be("Alice");
        session.CompletedAt.Should().NotBeNull();
        session.CompletedAt!.Value.Should().BeOnOrAfter(beforeComplete).And.BeOnOrBefore(afterComplete);
    }

    [Fact]
    public void GameSession_Complete_WithoutWinner_WorksCorrectly()
    {
        // Arrange
        var session = CreateDefaultSession();
        session.Start();

        // Act
        session.Complete();

        // Assert
        session.Status.Should().Be(SessionStatus.Completed);
        session.WinnerName.Should().BeNull();
        session.CompletedAt.Should().NotBeNull();
    }

    [Fact]
    public void GameSession_Complete_WhenNotInProgress_ThrowsInvalidOperationException()
    {
        // Arrange
        var session = CreateDefaultSession(); // Status = Setup

        // Act & Assert
        var act = () => session.Complete();
        var exception = act.Should().Throw<InvalidOperationException>().Which;
        exception.Message.Should().ContainEquivalentOf("must be InProgress");
    }

    [Fact]
    public void GameSession_Complete_WithTooLongWinnerName_ThrowsValidationException()
    {
        // Arrange
        var session = CreateDefaultSession();
        session.Start();
        var longName = new string('A', 51); // 51 characters exceeds the 50 character limit

        // Act & Assert
        var act = () => session.Complete(longName);
        var exception = act.Should().Throw<Api.SharedKernel.Domain.Exceptions.ValidationException>().Which;
        exception.Message.Should().ContainEquivalentOf("Winner name cannot exceed 50 characters");
    }

    [Fact]
    public void GameSession_Complete_WithExactly50CharWinnerName_Succeeds()
    {
        // Arrange
        var session = CreateDefaultSession();
        session.Start();
        var maxLengthName = new string('A', 50); // Exactly 50 characters

        // Act
        session.Complete(maxLengthName);

        // Assert
        session.Status.Should().Be(SessionStatus.Completed);
        session.WinnerName.Should().Be(maxLengthName);
    }

    [Fact]
    public void GameSession_Abandon_SetsStatusAndTimestamp()
    {
        // Arrange
        var session = CreateDefaultSession();
        session.Start();

        // Act
        session.Abandon("Players had to leave");

        // Assert
        session.Status.Should().Be(SessionStatus.Abandoned);
        session.CompletedAt.Should().NotBeNull();
        session.Notes.Should().ContainEquivalentOf("Abandoned: Players had to leave");
    }

    [Fact]
    public void GameSession_Abandon_WhenAlreadyCompleted_ThrowsInvalidOperationException()
    {
        // Arrange
        var session = CreateDefaultSession();
        session.Start();
        session.Complete();

        // Act & Assert
        var act = () => session.Abandon();
        var exception = act.Should().Throw<InvalidOperationException>().Which;
        exception.Message.Should().ContainEquivalentOf("Cannot abandon finished session");
    }

    [Fact]
    public void GameSession_AddNotes_AppendsToExisting()
    {
        // Arrange
        var session = CreateDefaultSession();
        session.AddNotes("First note");

        // Act
        session.AddNotes("Second note");

        // Assert
        session.Notes.Should().Be("First note\nSecond note");
    }

    [Fact]
    public void GameSession_AddNotes_IgnoresEmptyString()
    {
        // Arrange
        var session = CreateDefaultSession();

        // Act
        session.AddNotes("   ");

        // Assert
        session.Notes.Should().BeNull();
    }

    [Fact]
    public void GameSession_Duration_CalculatesCorrectly()
    {
        // Arrange
        var session = CreateDefaultSession();
        var startTime = DateTime.UtcNow;

        // Act
        var duration = session.Duration;

        // Assert
        duration.Should().BeGreaterThanOrEqualTo(TimeSpan.Zero).And.BeLessThanOrEqualTo(TestConstants.Timing.MediumTimeout);
    }

    [Fact]
    public void GameSession_HasPlayer_FindsPlayerCaseInsensitive()
    {
        // Arrange
        var players = new List<SessionPlayer>
        {
            new SessionPlayer("Alice", 1),
            new SessionPlayer("Bob", 2)
        };
        var session = new GameSession(Guid.NewGuid(), Guid.NewGuid(), players);

        // Act & Assert
        session.HasPlayer("Alice").Should().BeTrue();
        session.HasPlayer("ALICE").Should().BeTrue();
        session.HasPlayer("alice").Should().BeTrue();
        session.HasPlayer("Charlie").Should().BeFalse();
    }

    [Fact]
    public void GameSession_HasPlayer_WithEmptyName_ReturnsFalse()
    {
        // Arrange
        var session = CreateDefaultSession();

        // Act & Assert
        session.HasPlayer("").Should().BeFalse();
        session.HasPlayer("   ").Should().BeFalse();
    }

    // ========================================
    // AddPlayer Tests
    // ========================================

    [Fact]
    public void GameSession_AddPlayer_InSetupStatus_AddsSuccessfully()
    {
        // Arrange
        var session = CreateDefaultSession(); // Status = Setup
        var newPlayer = new SessionPlayer("Charlie", 3, "Green");

        // Act
        session.AddPlayer(newPlayer);

        // Assert
        session.PlayerCount.Should().Be(3);
        session.HasPlayer("Charlie").Should().BeTrue();
        session.Players.Should().Contain(p => p.PlayerName == "Charlie");
    }

    [Fact]
    public void GameSession_AddPlayer_InProgressStatus_AddsSuccessfully()
    {
        // Arrange
        var session = CreateDefaultSession();
        session.Start(); // Status = InProgress
        var newPlayer = new SessionPlayer("Charlie", 3, "Green");

        // Act
        session.AddPlayer(newPlayer);

        // Assert
        session.PlayerCount.Should().Be(3);
        session.HasPlayer("Charlie").Should().BeTrue();
    }

    [Fact]
    public void GameSession_AddPlayer_NullPlayer_ThrowsArgumentNullException()
    {
        // Arrange
        var session = CreateDefaultSession();

        // Act & Assert
        var act = () => session.AddPlayer(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void GameSession_AddPlayer_ToCompletedSession_ThrowsInvalidOperationException()
    {
        // Arrange
        var session = CreateDefaultSession();
        session.Start();
        session.Complete();
        var newPlayer = new SessionPlayer("Charlie", 3);

        // Act & Assert
        var act = () => session.AddPlayer(newPlayer);
        var exception = act.Should().Throw<InvalidOperationException>().Which;
        exception.Message.Should().ContainEquivalentOf("Cannot add player to finished session");
    }

    [Fact]
    public void GameSession_AddPlayer_ToAbandonedSession_ThrowsInvalidOperationException()
    {
        // Arrange
        var session = CreateDefaultSession();
        session.Start();
        session.Abandon();
        var newPlayer = new SessionPlayer("Charlie", 3);

        // Act & Assert
        var act = () => session.AddPlayer(newPlayer);
        var exception = act.Should().Throw<InvalidOperationException>().Which;
        exception.Message.Should().Contain("Cannot add player to finished session");
    }

    [Fact]
    public void GameSession_AddPlayer_DuplicateName_ThrowsInvalidOperationException()
    {
        // Arrange
        var session = CreateDefaultSession(); // Has "Alice" and "Bob"
        var duplicatePlayer = new SessionPlayer("Alice", 3); // Same name as existing

        // Act & Assert
        var act = () => session.AddPlayer(duplicatePlayer);
        var exception = act.Should().Throw<InvalidOperationException>().Which;
        exception.Message.Should().ContainEquivalentOf("already in this session");
        exception.Message.Should().ContainEquivalentOf("Alice");
    }

    [Fact]
    public void GameSession_AddPlayer_DuplicateNameCaseInsensitive_ThrowsInvalidOperationException()
    {
        // Arrange
        var session = CreateDefaultSession(); // Has "Alice"
        var duplicatePlayer = new SessionPlayer("ALICE", 3); // Different case

        // Act & Assert
        var act = () => session.AddPlayer(duplicatePlayer);
        var exception = act.Should().Throw<InvalidOperationException>().Which;
        exception.Message.Should().ContainEquivalentOf("already in this session");
    }

    [Fact]
    public void GameSession_AddPlayer_Exceeds100Players_ThrowsInvalidOperationException()
    {
        // Arrange - Create session with 100 players
        var players = Enumerable.Range(1, 100)
            .Select(i => new SessionPlayer($"Player{i}", 1))
            .ToList();
        var session = new GameSession(Guid.NewGuid(), Guid.NewGuid(), players);

        var oneMorePlayer = new SessionPlayer("Player101", 1);

        // Act & Assert
        var act = () => session.AddPlayer(oneMorePlayer);
        var exception = act.Should().Throw<InvalidOperationException>().Which;
        exception.Message.Should().ContainEquivalentOf("cannot have more than 100 players");
    }

    [Fact]
    public void GameSession_AddPlayer_MultiplePlayersSequentially_AllAdded()
    {
        // Arrange
        var session = CreateDefaultSession(); // Starts with 2 players

        // Act
        session.AddPlayer(new SessionPlayer("Charlie", 3, "Green"));
        session.AddPlayer(new SessionPlayer("Diana", 4, "Yellow"));
        session.AddPlayer(new SessionPlayer("Eve", 5, "Purple"));

        // Assert
        session.PlayerCount.Should().Be(5);
        session.HasPlayer("Charlie").Should().BeTrue();
        session.HasPlayer("Diana").Should().BeTrue();
        session.HasPlayer("Eve").Should().BeTrue();
    }

    [Fact]
    public void GameSession_AddPlayer_ToPausedSession_AddsSuccessfully()
    {
        // Arrange
        var session = CreateDefaultSession();
        session.Start();
        session.Pause(); // Status = Paused
        var newPlayer = new SessionPlayer("Charlie", 3, "Green");

        // Act
        session.AddPlayer(newPlayer);

        // Assert
        session.PlayerCount.Should().Be(3);
        session.HasPlayer("Charlie").Should().BeTrue();
    }

    private static GameSession CreateDefaultSession()
    {
        var players = new List<SessionPlayer>
        {
            new SessionPlayer("Alice", 1, "Red"),
            new SessionPlayer("Bob", 2, "Blue")
        };

        return new GameSession(Guid.NewGuid(), Guid.NewGuid(), players);
    }
}

