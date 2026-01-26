using Api.BoundedContexts.GameManagement.Domain.Entities;
using Api.BoundedContexts.GameManagement.Domain.Events;
using Api.BoundedContexts.GameManagement.Domain.ValueObjects;
using Api.SharedKernel.Domain.Exceptions;
using FluentAssertions;
using Xunit;

namespace Api.Tests.GameManagement.Domain.Entities;

[Trait("Category", "Unit")]
public sealed class GameSessionTests
{
    private static SessionPlayer CreatePlayer(string name, int order) => new(name, order);
    private static List<SessionPlayer> CreatePlayers(int count) =>
        Enumerable.Range(1, count).Select(i => CreatePlayer($"Player{i}", i)).ToList();

    #region Constructor Tests

    [Fact]
    public void Constructor_WithValidParameters_CreatesSession()
    {
        // Arrange
        var id = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var players = CreatePlayers(2);

        // Act
        var session = new GameSession(id, gameId, players);

        // Assert
        session.Id.Should().Be(id);
        session.GameId.Should().Be(gameId);
        session.Status.Should().Be(SessionStatus.Setup);
        session.Players.Should().HaveCount(2);
        session.CompletedAt.Should().BeNull();
        session.WinnerName.Should().BeNull();
        session.Notes.Should().BeNull();
        session.StartedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void Constructor_RaisesGameSessionCreatedEvent()
    {
        // Arrange
        var id = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var players = CreatePlayers(3);

        // Act
        var session = new GameSession(id, gameId, players);

        // Assert
        session.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<GameSessionCreatedEvent>()
            .Which.SessionId.Should().Be(id);
    }

    [Fact]
    public void Constructor_WithEmptyGameId_ThrowsArgumentException()
    {
        // Arrange
        var players = CreatePlayers(1);

        // Act & Assert
        var action = () => new GameSession(Guid.NewGuid(), Guid.Empty, players);
        action.Should().Throw<ArgumentException>()
            .WithMessage("*GameId cannot be empty*");
    }

    [Fact]
    public void Constructor_WithNullPlayers_ThrowsArgumentNullException()
    {
        // Act & Assert
        var action = () => new GameSession(Guid.NewGuid(), Guid.NewGuid(), null!);
        action.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_WithNoPlayers_ThrowsArgumentException()
    {
        // Arrange
        var emptyPlayers = new List<SessionPlayer>();

        // Act & Assert
        var action = () => new GameSession(Guid.NewGuid(), Guid.NewGuid(), emptyPlayers);
        action.Should().Throw<ArgumentException>()
            .WithMessage("*Session must have at least one player*");
    }

    [Fact]
    public void Constructor_WithTooManyPlayers_ThrowsArgumentException()
    {
        // Arrange - Create 101 players with valid orders (cycling 1-100)
        // SessionPlayer validates order 1-100, so we need to cycle through valid orders
        var tooManyPlayers = Enumerable.Range(0, 101)
            .Select(i => CreatePlayer($"Player{i}", (i % 100) + 1))
            .ToList();

        // Act & Assert
        var action = () => new GameSession(Guid.NewGuid(), Guid.NewGuid(), tooManyPlayers);
        action.Should().Throw<ArgumentException>()
            .WithMessage("*Session cannot have more than 100 players*");
    }

    [Fact]
    public void Constructor_With100Players_Succeeds()
    {
        // Arrange
        var maxPlayers = Enumerable.Range(1, 100)
            .Select(i => CreatePlayer($"Player{i}", i))
            .ToList();

        // Act
        var session = new GameSession(Guid.NewGuid(), Guid.NewGuid(), maxPlayers);

        // Assert
        session.PlayerCount.Should().Be(100);
    }

    #endregion

    #region Status Transition Tests - Start

    [Fact]
    public void Start_FromSetup_TransitionsToInProgress()
    {
        // Arrange
        var session = new GameSession(Guid.NewGuid(), Guid.NewGuid(), CreatePlayers(2));

        // Act
        session.Start();

        // Assert
        session.Status.Should().Be(SessionStatus.InProgress);
    }

    [Fact]
    public void Start_RaisesGameSessionStartedEvent()
    {
        // Arrange
        var session = new GameSession(Guid.NewGuid(), Guid.NewGuid(), CreatePlayers(2));
        session.ClearDomainEvents(); // Clear constructor event

        // Act
        session.Start();

        // Assert
        session.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<GameSessionStartedEvent>();
    }

    [Fact]
    public void Start_FromInProgress_ThrowsInvalidOperationException()
    {
        // Arrange
        var session = CreateSessionInStatus(SessionStatus.InProgress);

        // Act & Assert
        var action = () => session.Start();
        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*Cannot start session*");
    }

    [Fact]
    public void Start_FromPaused_ThrowsInvalidOperationException()
    {
        // Arrange
        var session = CreateSessionInStatus(SessionStatus.Paused);

        // Act & Assert
        var action = () => session.Start();
        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*Cannot start session*");
    }

    [Fact]
    public void Start_FromCompleted_ThrowsInvalidOperationException()
    {
        // Arrange
        var session = CreateSessionInStatus(SessionStatus.Completed);

        // Act & Assert
        var action = () => session.Start();
        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*Cannot start session*");
    }

    [Fact]
    public void Start_FromAbandoned_ThrowsInvalidOperationException()
    {
        // Arrange
        var session = CreateSessionInStatus(SessionStatus.Abandoned);

        // Act & Assert
        var action = () => session.Start();
        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*Cannot start session*");
    }

    #endregion

    #region Status Transition Tests - Pause

    [Fact]
    public void Pause_FromInProgress_TransitionsToPaused()
    {
        // Arrange
        var session = CreateSessionInStatus(SessionStatus.InProgress);

        // Act
        session.Pause();

        // Assert
        session.Status.Should().Be(SessionStatus.Paused);
    }

    [Fact]
    public void Pause_RaisesGameSessionPausedEvent()
    {
        // Arrange
        var session = CreateSessionInStatus(SessionStatus.InProgress);
        session.ClearDomainEvents();

        // Act
        session.Pause();

        // Assert
        session.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<GameSessionPausedEvent>();
    }

    [Fact]
    public void Pause_FromSetup_ThrowsInvalidOperationException()
    {
        // Arrange
        var session = new GameSession(Guid.NewGuid(), Guid.NewGuid(), CreatePlayers(2));

        // Act & Assert
        var action = () => session.Pause();
        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*Cannot pause session*");
    }

    [Fact]
    public void Pause_FromPaused_ThrowsInvalidOperationException()
    {
        // Arrange
        var session = CreateSessionInStatus(SessionStatus.Paused);

        // Act & Assert
        var action = () => session.Pause();
        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*Cannot pause session*");
    }

    [Fact]
    public void Pause_FromCompleted_ThrowsInvalidOperationException()
    {
        // Arrange
        var session = CreateSessionInStatus(SessionStatus.Completed);

        // Act & Assert
        var action = () => session.Pause();
        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*Cannot pause session*");
    }

    [Fact]
    public void Pause_FromAbandoned_ThrowsInvalidOperationException()
    {
        // Arrange
        var session = CreateSessionInStatus(SessionStatus.Abandoned);

        // Act & Assert
        var action = () => session.Pause();
        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*Cannot pause session*");
    }

    #endregion

    #region Status Transition Tests - Resume

    [Fact]
    public void Resume_FromPaused_TransitionsToInProgress()
    {
        // Arrange
        var session = CreateSessionInStatus(SessionStatus.Paused);

        // Act
        session.Resume();

        // Assert
        session.Status.Should().Be(SessionStatus.InProgress);
    }

    [Fact]
    public void Resume_RaisesGameSessionResumedEvent()
    {
        // Arrange
        var session = CreateSessionInStatus(SessionStatus.Paused);
        session.ClearDomainEvents();

        // Act
        session.Resume();

        // Assert
        session.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<GameSessionResumedEvent>();
    }

    [Fact]
    public void Resume_FromSetup_ThrowsInvalidOperationException()
    {
        // Arrange
        var session = new GameSession(Guid.NewGuid(), Guid.NewGuid(), CreatePlayers(2));

        // Act & Assert
        var action = () => session.Resume();
        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*Cannot resume session*");
    }

    [Fact]
    public void Resume_FromInProgress_ThrowsInvalidOperationException()
    {
        // Arrange
        var session = CreateSessionInStatus(SessionStatus.InProgress);

        // Act & Assert
        var action = () => session.Resume();
        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*Cannot resume session*");
    }

    [Fact]
    public void Resume_FromCompleted_ThrowsInvalidOperationException()
    {
        // Arrange
        var session = CreateSessionInStatus(SessionStatus.Completed);

        // Act & Assert
        var action = () => session.Resume();
        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*Cannot resume session*");
    }

    [Fact]
    public void Resume_FromAbandoned_ThrowsInvalidOperationException()
    {
        // Arrange
        var session = CreateSessionInStatus(SessionStatus.Abandoned);

        // Act & Assert
        var action = () => session.Resume();
        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*Cannot resume session*");
    }

    #endregion

    #region Status Transition Tests - Complete

    [Fact]
    public void Complete_FromInProgress_TransitionsToCompleted()
    {
        // Arrange
        var session = CreateSessionInStatus(SessionStatus.InProgress);

        // Act
        session.Complete();

        // Assert
        session.Status.Should().Be(SessionStatus.Completed);
        session.CompletedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void Complete_FromPaused_TransitionsToCompleted()
    {
        // Arrange
        var session = CreateSessionInStatus(SessionStatus.Paused);

        // Act
        session.Complete();

        // Assert
        session.Status.Should().Be(SessionStatus.Completed);
    }

    [Fact]
    public void Complete_WithWinner_SetsWinnerName()
    {
        // Arrange
        var session = CreateSessionInStatus(SessionStatus.InProgress);

        // Act
        session.Complete("Player1");

        // Assert
        session.WinnerName.Should().Be("Player1");
    }

    [Fact]
    public void Complete_WithNullWinner_SetsWinnerToNull()
    {
        // Arrange
        var session = CreateSessionInStatus(SessionStatus.InProgress);

        // Act
        session.Complete(null);

        // Assert
        session.WinnerName.Should().BeNull();
    }

    [Fact]
    public void Complete_TrimsWinnerName()
    {
        // Arrange
        var session = CreateSessionInStatus(SessionStatus.InProgress);

        // Act
        session.Complete("  Winner  ");

        // Assert
        session.WinnerName.Should().Be("Winner");
    }

    [Fact]
    public void Complete_WithTooLongWinnerName_ThrowsValidationException()
    {
        // Arrange
        var session = CreateSessionInStatus(SessionStatus.InProgress);
        var longName = new string('A', 51);

        // Act & Assert
        var action = () => session.Complete(longName);
        action.Should().Throw<ValidationException>()
            .WithMessage("*Winner name cannot exceed 50 characters*");
    }

    [Fact]
    public void Complete_RaisesGameSessionCompletedEvent()
    {
        // Arrange
        var session = CreateSessionInStatus(SessionStatus.InProgress);
        session.ClearDomainEvents();

        // Act
        session.Complete("Winner");

        // Assert
        session.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<GameSessionCompletedEvent>();
    }

    [Fact]
    public void Complete_FromSetup_ThrowsInvalidOperationException()
    {
        // Arrange
        var session = new GameSession(Guid.NewGuid(), Guid.NewGuid(), CreatePlayers(2));

        // Act & Assert
        var action = () => session.Complete();
        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*Cannot complete session*");
    }

    #endregion

    #region Status Transition Tests - Abandon

    [Fact]
    public void Abandon_FromSetup_TransitionsToAbandoned()
    {
        // Arrange
        var session = new GameSession(Guid.NewGuid(), Guid.NewGuid(), CreatePlayers(2));

        // Act
        session.Abandon();

        // Assert
        session.Status.Should().Be(SessionStatus.Abandoned);
        session.CompletedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void Abandon_FromInProgress_TransitionsToAbandoned()
    {
        // Arrange
        var session = CreateSessionInStatus(SessionStatus.InProgress);

        // Act
        session.Abandon();

        // Assert
        session.Status.Should().Be(SessionStatus.Abandoned);
    }

    [Fact]
    public void Abandon_FromPaused_TransitionsToAbandoned()
    {
        // Arrange
        var session = CreateSessionInStatus(SessionStatus.Paused);

        // Act
        session.Abandon();

        // Assert
        session.Status.Should().Be(SessionStatus.Abandoned);
    }

    [Fact]
    public void Abandon_WithReason_AddsReasonToNotes()
    {
        // Arrange
        var session = CreateSessionInStatus(SessionStatus.InProgress);

        // Act
        session.Abandon("Network disconnection");

        // Assert
        session.Notes.Should().Contain("Abandoned: Network disconnection");
    }

    [Fact]
    public void Abandon_WithReason_AppendsToExistingNotes()
    {
        // Arrange
        var session = CreateSessionInStatus(SessionStatus.InProgress);
        session.AddNotes("Game was going well");

        // Act
        session.Abandon("Player left");

        // Assert
        session.Notes.Should().Contain("Game was going well");
        session.Notes.Should().Contain("Abandoned: Player left");
    }

    [Fact]
    public void Abandon_RaisesGameSessionAbandonedEvent()
    {
        // Arrange
        var session = CreateSessionInStatus(SessionStatus.InProgress);
        session.ClearDomainEvents();

        // Act
        session.Abandon("Reason");

        // Assert
        session.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<GameSessionAbandonedEvent>();
    }

    [Fact]
    public void Abandon_FromCompleted_ThrowsInvalidOperationException()
    {
        // Arrange
        var session = CreateSessionInStatus(SessionStatus.Completed);

        // Act & Assert
        var action = () => session.Abandon();
        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*Cannot abandon finished session*");
    }

    [Fact]
    public void Abandon_FromAbandoned_ThrowsInvalidOperationException()
    {
        // Arrange
        var session = CreateSessionInStatus(SessionStatus.Abandoned);

        // Act & Assert
        var action = () => session.Abandon();
        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*Cannot abandon finished session*");
    }

    #endregion

    #region AddPlayer Tests

    [Fact]
    public void AddPlayer_ToActiveSession_AddsPlayer()
    {
        // Arrange
        var session = new GameSession(Guid.NewGuid(), Guid.NewGuid(), CreatePlayers(1));
        var newPlayer = CreatePlayer("NewPlayer", 2);

        // Act
        session.AddPlayer(newPlayer);

        // Assert
        session.Players.Should().HaveCount(2);
        session.PlayerCount.Should().Be(2);
    }

    [Fact]
    public void AddPlayer_RaisesPlayerAddedToSessionEvent()
    {
        // Arrange
        var session = new GameSession(Guid.NewGuid(), Guid.NewGuid(), CreatePlayers(1));
        session.ClearDomainEvents();
        var newPlayer = CreatePlayer("NewPlayer", 2);

        // Act
        session.AddPlayer(newPlayer);

        // Assert
        session.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<PlayerAddedToSessionEvent>();
    }

    [Fact]
    public void AddPlayer_WithNull_ThrowsArgumentNullException()
    {
        // Arrange
        var session = new GameSession(Guid.NewGuid(), Guid.NewGuid(), CreatePlayers(1));

        // Act & Assert
        var action = () => session.AddPlayer(null!);
        action.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void AddPlayer_WhenAtMaxCapacity_ThrowsInvalidOperationException()
    {
        // Arrange
        var maxPlayers = Enumerable.Range(1, 100)
            .Select(i => CreatePlayer($"Player{i}", i))
            .ToList();
        var session = new GameSession(Guid.NewGuid(), Guid.NewGuid(), maxPlayers);

        // Act & Assert - Use valid order (1) since SessionPlayer limits order to 1-100
        var action = () => session.AddPlayer(CreatePlayer("ExtraPlayer", 1));
        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*Session cannot have more than 100 players*");
    }

    [Fact]
    public void AddPlayer_DuplicateName_ThrowsInvalidOperationException()
    {
        // Arrange
        var session = new GameSession(Guid.NewGuid(), Guid.NewGuid(), CreatePlayers(1));
        var duplicatePlayer = CreatePlayer("Player1", 2); // Same name as existing

        // Act & Assert
        var action = () => session.AddPlayer(duplicatePlayer);
        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*Player 'Player1' is already in this session*");
    }

    [Fact]
    public void AddPlayer_ToCompletedSession_ThrowsInvalidOperationException()
    {
        // Arrange
        var session = CreateSessionInStatus(SessionStatus.Completed);

        // Act & Assert
        var action = () => session.AddPlayer(CreatePlayer("New", 5));
        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*Cannot add player to finished session*");
    }

    [Fact]
    public void AddPlayer_ToAbandonedSession_ThrowsInvalidOperationException()
    {
        // Arrange
        var session = CreateSessionInStatus(SessionStatus.Abandoned);

        // Act & Assert
        var action = () => session.AddPlayer(CreatePlayer("New", 5));
        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*Cannot add player to finished session*");
    }

    #endregion

    #region HasPlayer Tests

    [Fact]
    public void HasPlayer_WithExistingPlayer_ReturnsTrue()
    {
        // Arrange
        var session = new GameSession(Guid.NewGuid(), Guid.NewGuid(), CreatePlayers(2));

        // Act & Assert
        session.HasPlayer("Player1").Should().BeTrue();
        session.HasPlayer("Player2").Should().BeTrue();
    }

    [Fact]
    public void HasPlayer_IsCaseInsensitive()
    {
        // Arrange
        var session = new GameSession(Guid.NewGuid(), Guid.NewGuid(), CreatePlayers(1));

        // Act & Assert
        session.HasPlayer("PLAYER1").Should().BeTrue();
        session.HasPlayer("player1").Should().BeTrue();
    }

    [Fact]
    public void HasPlayer_WithNonExistingPlayer_ReturnsFalse()
    {
        // Arrange
        var session = new GameSession(Guid.NewGuid(), Guid.NewGuid(), CreatePlayers(1));

        // Act & Assert
        session.HasPlayer("NonExistent").Should().BeFalse();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void HasPlayer_WithInvalidName_ReturnsFalse(string? invalidName)
    {
        // Arrange
        var session = new GameSession(Guid.NewGuid(), Guid.NewGuid(), CreatePlayers(1));

        // Act & Assert
        session.HasPlayer(invalidName!).Should().BeFalse();
    }

    [Fact]
    public void HasPlayer_TrimsInputName()
    {
        // Arrange
        var session = new GameSession(Guid.NewGuid(), Guid.NewGuid(), CreatePlayers(1));

        // Act & Assert
        session.HasPlayer("  Player1  ").Should().BeTrue();
    }

    #endregion

    #region AddNotes Tests

    [Fact]
    public void AddNotes_ToEmptyNotes_SetsNotes()
    {
        // Arrange
        var session = new GameSession(Guid.NewGuid(), Guid.NewGuid(), CreatePlayers(1));

        // Act
        session.AddNotes("First note");

        // Assert
        session.Notes.Should().Be("First note");
    }

    [Fact]
    public void AddNotes_ToExistingNotes_AppendsWithNewline()
    {
        // Arrange
        var session = new GameSession(Guid.NewGuid(), Guid.NewGuid(), CreatePlayers(1));
        session.AddNotes("First note");

        // Act
        session.AddNotes("Second note");

        // Assert
        session.Notes.Should().Be("First note\nSecond note");
    }

    [Fact]
    public void AddNotes_TrimsInput()
    {
        // Arrange
        var session = new GameSession(Guid.NewGuid(), Guid.NewGuid(), CreatePlayers(1));

        // Act
        session.AddNotes("  Note with spaces  ");

        // Assert
        session.Notes.Should().Be("Note with spaces");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void AddNotes_WithEmptyInput_DoesNothing(string? emptyInput)
    {
        // Arrange
        var session = new GameSession(Guid.NewGuid(), Guid.NewGuid(), CreatePlayers(1));
        session.AddNotes("Existing note");

        // Act
        session.AddNotes(emptyInput!);

        // Assert
        session.Notes.Should().Be("Existing note");
    }

    #endregion

    #region Duration Tests

    [Fact]
    public void Duration_ForActiveSession_ReturnsElapsedTime()
    {
        // Arrange
        var session = new GameSession(Guid.NewGuid(), Guid.NewGuid(), CreatePlayers(1));

        // Act
        var duration = session.Duration;

        // Assert
        duration.Should().BeCloseTo(TimeSpan.Zero, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void Duration_ForCompletedSession_ReturnsFixedDuration()
    {
        // Arrange
        var session = CreateSessionInStatus(SessionStatus.InProgress);
        Thread.Sleep(100); // Small delay
        session.Complete();

        // Act
        var duration1 = session.Duration;
        Thread.Sleep(100);
        var duration2 = session.Duration;

        // Assert - Duration should be fixed after completion
        duration1.Should().Be(duration2);
    }

    [Fact]
    public void DurationMinutes_ReturnsNonNegative()
    {
        // Arrange
        var session = new GameSession(Guid.NewGuid(), Guid.NewGuid(), CreatePlayers(1));

        // Act & Assert
        session.DurationMinutes.Should().BeGreaterThanOrEqualTo(0);
    }

    #endregion

    #region Helper Methods

    private static GameSession CreateSessionInStatus(SessionStatus targetStatus)
    {
        var session = new GameSession(Guid.NewGuid(), Guid.NewGuid(), CreatePlayers(2));

        if (targetStatus == SessionStatus.Setup)
            return session;

        session.Start();
        if (targetStatus == SessionStatus.InProgress)
            return session;

        if (targetStatus == SessionStatus.Paused)
        {
            session.Pause();
            return session;
        }

        if (targetStatus == SessionStatus.Completed)
        {
            session.Complete();
            return session;
        }

        if (targetStatus == SessionStatus.Abandoned)
        {
            session.Abandon();
            return session;
        }

        throw new ArgumentException($"Unknown status: {targetStatus}");
    }

    #endregion
}
