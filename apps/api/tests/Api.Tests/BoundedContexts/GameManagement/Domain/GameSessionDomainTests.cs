using Api.BoundedContexts.GameManagement.Domain.Entities;
using Api.BoundedContexts.GameManagement.Domain.ValueObjects;
using Xunit;

namespace Api.Tests.BoundedContexts.GameManagement.Domain;

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
        Assert.Equal(sessionId, session.Id);
        Assert.Equal(gameId, session.GameId);
        Assert.Equal(SessionStatus.Setup, session.Status);
        Assert.Equal(2, session.PlayerCount);
        Assert.Equal(2, session.Players.Count);
    }

    [Fact]
    public void GameSession_EmptyGameId_ThrowsArgumentException()
    {
        // Arrange
        var players = new List<SessionPlayer> { new SessionPlayer("Alice", 1) };

        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() =>
            new GameSession(Guid.NewGuid(), Guid.Empty, players));
        Assert.Contains("GameId cannot be empty", exception.Message);
    }

    [Fact]
    public void GameSession_NullPlayers_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new GameSession(Guid.NewGuid(), Guid.NewGuid(), null!));
    }

    [Fact]
    public void GameSession_EmptyPlayersList_ThrowsArgumentException()
    {
        // Arrange
        var players = new List<SessionPlayer>();

        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() =>
            new GameSession(Guid.NewGuid(), Guid.NewGuid(), players));
        Assert.Contains("at least one player", exception.Message);
    }

    [Fact]
    public void GameSession_TooManyPlayers_ThrowsArgumentException()
    {
        // Arrange - Create 101 players (all with same PlayerOrder to avoid VO validation)
        var players = Enumerable.Range(1, 101)
            .Select(i => new SessionPlayer($"Player{i}", 1)) // All order=1 to bypass VO validation
            .ToList();

        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() =>
            new GameSession(Guid.NewGuid(), Guid.NewGuid(), players));
        Assert.Contains("cannot have more than 100 players", exception.Message);
    }

    [Fact]
    public void GameSession_Start_MovesFromSetupToInProgress()
    {
        // Arrange
        var session = CreateDefaultSession();

        // Act
        session.Start();

        // Assert
        Assert.Equal(SessionStatus.InProgress, session.Status);
    }

    [Fact]
    public void GameSession_Start_WhenAlreadyStarted_ThrowsInvalidOperationException()
    {
        // Arrange
        var session = CreateDefaultSession();
        session.Start();

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() => session.Start());
        Assert.Contains("must be in Setup", exception.Message);
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
        Assert.Equal(SessionStatus.Completed, session.Status);
        Assert.Equal("Alice", session.WinnerName);
        Assert.NotNull(session.CompletedAt);
        Assert.InRange(session.CompletedAt.Value, beforeComplete, afterComplete);
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
        Assert.Equal(SessionStatus.Completed, session.Status);
        Assert.Null(session.WinnerName);
        Assert.NotNull(session.CompletedAt);
    }

    [Fact]
    public void GameSession_Complete_WhenNotInProgress_ThrowsInvalidOperationException()
    {
        // Arrange
        var session = CreateDefaultSession(); // Status = Setup

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() => session.Complete());
        Assert.Contains("must be InProgress", exception.Message);
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
        Assert.Equal(SessionStatus.Abandoned, session.Status);
        Assert.NotNull(session.CompletedAt);
        Assert.Contains("Abandoned: Players had to leave", session.Notes);
    }

    [Fact]
    public void GameSession_Abandon_WhenAlreadyCompleted_ThrowsInvalidOperationException()
    {
        // Arrange
        var session = CreateDefaultSession();
        session.Start();
        session.Complete();

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() => session.Abandon());
        Assert.Contains("Cannot abandon finished session", exception.Message);
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
        Assert.Equal("First note\nSecond note", session.Notes);
    }

    [Fact]
    public void GameSession_AddNotes_IgnoresEmptyString()
    {
        // Arrange
        var session = CreateDefaultSession();

        // Act
        session.AddNotes("   ");

        // Assert
        Assert.Null(session.Notes);
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
        Assert.InRange(duration, TimeSpan.Zero, TimeSpan.FromSeconds(2));
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
        Assert.True(session.HasPlayer("Alice"));
        Assert.True(session.HasPlayer("ALICE"));
        Assert.True(session.HasPlayer("alice"));
        Assert.False(session.HasPlayer("Charlie"));
    }

    [Fact]
    public void GameSession_HasPlayer_WithEmptyName_ReturnsFalse()
    {
        // Arrange
        var session = CreateDefaultSession();

        // Act & Assert
        Assert.False(session.HasPlayer(""));
        Assert.False(session.HasPlayer("   "));
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
        Assert.Equal(3, session.PlayerCount);
        Assert.True(session.HasPlayer("Charlie"));
        Assert.Contains(session.Players, p => p.PlayerName == "Charlie");
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
        Assert.Equal(3, session.PlayerCount);
        Assert.True(session.HasPlayer("Charlie"));
    }

    [Fact]
    public void GameSession_AddPlayer_NullPlayer_ThrowsArgumentNullException()
    {
        // Arrange
        var session = CreateDefaultSession();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => session.AddPlayer(null!));
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
        var exception = Assert.Throws<InvalidOperationException>(() => session.AddPlayer(newPlayer));
        Assert.Contains("Cannot add player to finished session", exception.Message);
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
        var exception = Assert.Throws<InvalidOperationException>(() => session.AddPlayer(newPlayer));
        Assert.Contains("Cannot add player to finished session", exception.Message);
    }

    [Fact]
    public void GameSession_AddPlayer_DuplicateName_ThrowsInvalidOperationException()
    {
        // Arrange
        var session = CreateDefaultSession(); // Has "Alice" and "Bob"
        var duplicatePlayer = new SessionPlayer("Alice", 3); // Same name as existing

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() => session.AddPlayer(duplicatePlayer));
        Assert.Contains("already in this session", exception.Message);
        Assert.Contains("Alice", exception.Message);
    }

    [Fact]
    public void GameSession_AddPlayer_DuplicateNameCaseInsensitive_ThrowsInvalidOperationException()
    {
        // Arrange
        var session = CreateDefaultSession(); // Has "Alice"
        var duplicatePlayer = new SessionPlayer("ALICE", 3); // Different case

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() => session.AddPlayer(duplicatePlayer));
        Assert.Contains("already in this session", exception.Message);
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
        var exception = Assert.Throws<InvalidOperationException>(() => session.AddPlayer(oneMorePlayer));
        Assert.Contains("cannot have more than 100 players", exception.Message);
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
        Assert.Equal(5, session.PlayerCount);
        Assert.True(session.HasPlayer("Charlie"));
        Assert.True(session.HasPlayer("Diana"));
        Assert.True(session.HasPlayer("Eve"));
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
        Assert.Equal(3, session.PlayerCount);
        Assert.True(session.HasPlayer("Charlie"));
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
