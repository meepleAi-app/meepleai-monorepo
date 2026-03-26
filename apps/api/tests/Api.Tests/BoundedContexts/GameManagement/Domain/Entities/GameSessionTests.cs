using Api.BoundedContexts.GameManagement.Domain.Entities;
using Api.BoundedContexts.GameManagement.Domain.ValueObjects;
using Api.SharedKernel.Domain.Exceptions;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.GameManagement.Domain.Entities;

/// <summary>
/// Tests for the GameSession aggregate root.
/// Issue #3025: Backend 90% Coverage Target - Phase 17
/// </summary>
[Trait("Category", "Unit")]
public sealed class GameSessionTests
{
    #region Constructor Tests

    [Fact]
    public void Constructor_WithValidParameters_CreatesGameSession()
    {
        // Arrange
        var id = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var players = new List<SessionPlayer>
        {
            new("Alice", 1),
            new("Bob", 2)
        };

        // Act
        var session = new GameSession(id, gameId, players);

        // Assert
        session.Id.Should().Be(id);
        session.GameId.Should().Be(gameId);
        session.Status.Should().Be(SessionStatus.Setup);
        session.Players.Should().HaveCount(2);
        session.PlayerCount.Should().Be(2);
        session.CompletedAt.Should().BeNull();
        session.WinnerName.Should().BeNull();
    }

    [Fact]
    public void Constructor_WithEmptyGameId_ThrowsArgumentException()
    {
        // Arrange
        var id = Guid.NewGuid();
        var emptyGameId = Guid.Empty;
        var players = new List<SessionPlayer> { new("Player", 1) };

        // Act
        var action = () => new GameSession(id, emptyGameId, players);

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithParameterName("gameId")
            .WithMessage("*GameId cannot be empty*");
    }

    [Fact]
    public void Constructor_WithNullPlayers_ThrowsArgumentNullException()
    {
        // Arrange
        var id = Guid.NewGuid();
        var gameId = Guid.NewGuid();

        // Act
        var action = () => new GameSession(id, gameId, null!);

        // Assert
        action.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_WithEmptyPlayers_ThrowsArgumentException()
    {
        // Arrange
        var id = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var emptyPlayers = new List<SessionPlayer>();

        // Act
        var action = () => new GameSession(id, gameId, emptyPlayers);

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithMessage("*Session must have at least one player*");
    }

    [Fact]
    public void Constructor_WithTooManyPlayers_ThrowsArgumentException()
    {
        // Arrange
        var id = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        // PlayerOrder is limited to 1-100, so we use modulo to stay within bounds
        var tooManyPlayers = Enumerable.Range(1, 101)
            .Select(i => new SessionPlayer($"Player{i}", ((i - 1) % 100) + 1))
            .ToList();

        // Act
        var action = () => new GameSession(id, gameId, tooManyPlayers);

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithMessage("*Session cannot have more than 100 players*");
    }

    [Fact]
    public void Constructor_WithExactly100Players_Succeeds()
    {
        // Arrange
        var id = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var maxPlayers = Enumerable.Range(1, 100)
            .Select(i => new SessionPlayer($"Player{i}", i))
            .ToList();

        // Act
        var session = new GameSession(id, gameId, maxPlayers);

        // Assert
        session.PlayerCount.Should().Be(100);
    }

    [Fact]
    public void Constructor_RaisesGameSessionCreatedEvent()
    {
        // Arrange
        var id = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var players = new List<SessionPlayer> { new("Player", 1) };

        // Act
        var session = new GameSession(id, gameId, players);

        // Assert
        session.DomainEvents.Should().HaveCount(1);
    }

    #endregion

    #region Start Tests

    [Fact]
    public void Start_WhenSetup_TransitionsToInProgress()
    {
        // Arrange
        var session = CreateSetupSession();

        // Act
        session.Start();

        // Assert
        session.Status.Should().Be(SessionStatus.InProgress);
    }

    [Fact]
    public void Start_WhenInProgress_ThrowsInvalidOperationException()
    {
        // Arrange
        var session = CreateSetupSession();
        session.Start();

        // Act
        var action = () => session.Start();

        // Assert
        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*Cannot start session in InProgress status*");
    }

    [Fact]
    public void Start_WhenPaused_ThrowsInvalidOperationException()
    {
        // Arrange
        var session = CreateSetupSession();
        session.Start();
        session.Pause();

        // Act
        var action = () => session.Start();

        // Assert
        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*Cannot start session in Paused status*");
    }

    [Fact]
    public void Start_WhenCompleted_ThrowsInvalidOperationException()
    {
        // Arrange
        var session = CreateSetupSession();
        session.Start();
        session.Complete();

        // Act
        var action = () => session.Start();

        // Assert
        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*Cannot start session in Completed status*");
    }

    [Fact]
    public void Start_RaisesGameSessionStartedEvent()
    {
        // Arrange
        var session = CreateSetupSession();
        session.ClearDomainEvents();

        // Act
        session.Start();

        // Assert
        session.DomainEvents.Should().HaveCount(1);
    }

    #endregion

    #region Pause Tests

    [Fact]
    public void Pause_WhenInProgress_TransitionsToPaused()
    {
        // Arrange
        var session = CreateSetupSession();
        session.Start();

        // Act
        session.Pause();

        // Assert
        session.Status.Should().Be(SessionStatus.Paused);
    }

    [Fact]
    public void Pause_WhenSetup_ThrowsInvalidOperationException()
    {
        // Arrange
        var session = CreateSetupSession();

        // Act
        var action = () => session.Pause();

        // Assert
        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*Cannot pause session in Setup status*");
    }

    [Fact]
    public void Pause_WhenPaused_ThrowsInvalidOperationException()
    {
        // Arrange
        var session = CreateSetupSession();
        session.Start();
        session.Pause();

        // Act
        var action = () => session.Pause();

        // Assert
        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*Cannot pause session in Paused status*");
    }

    [Fact]
    public void Pause_RaisesGameSessionPausedEvent()
    {
        // Arrange
        var session = CreateSetupSession();
        session.Start();
        session.ClearDomainEvents();

        // Act
        session.Pause();

        // Assert
        session.DomainEvents.Should().HaveCount(1);
    }

    #endregion

    #region Resume Tests

    [Fact]
    public void Resume_WhenPaused_TransitionsToInProgress()
    {
        // Arrange
        var session = CreateSetupSession();
        session.Start();
        session.Pause();

        // Act
        session.Resume();

        // Assert
        session.Status.Should().Be(SessionStatus.InProgress);
    }

    [Fact]
    public void Resume_WhenSetup_ThrowsInvalidOperationException()
    {
        // Arrange
        var session = CreateSetupSession();

        // Act
        var action = () => session.Resume();

        // Assert
        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*Cannot resume session in Setup status*");
    }

    [Fact]
    public void Resume_WhenInProgress_ThrowsInvalidOperationException()
    {
        // Arrange
        var session = CreateSetupSession();
        session.Start();

        // Act
        var action = () => session.Resume();

        // Assert
        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*Cannot resume session in InProgress status*");
    }

    [Fact]
    public void Resume_RaisesGameSessionResumedEvent()
    {
        // Arrange
        var session = CreateSetupSession();
        session.Start();
        session.Pause();
        session.ClearDomainEvents();

        // Act
        session.Resume();

        // Assert
        session.DomainEvents.Should().HaveCount(1);
    }

    #endregion

    #region Complete Tests

    [Fact]
    public void Complete_WhenInProgress_TransitionsToCompleted()
    {
        // Arrange
        var session = CreateSetupSession();
        session.Start();

        // Act
        session.Complete();

        // Assert
        session.Status.Should().Be(SessionStatus.Completed);
        session.CompletedAt.Should().NotBeNull();
        session.Status.IsFinished.Should().BeTrue();
    }

    [Fact]
    public void Complete_WhenPaused_TransitionsToCompleted()
    {
        // Arrange
        var session = CreateSetupSession();
        session.Start();
        session.Pause();

        // Act
        session.Complete();

        // Assert
        session.Status.Should().Be(SessionStatus.Completed);
        session.CompletedAt.Should().NotBeNull();
    }

    [Fact]
    public void Complete_WithWinner_SetsWinnerName()
    {
        // Arrange
        var session = CreateSetupSession();
        session.Start();

        // Act
        session.Complete("Alice");

        // Assert
        session.WinnerName.Should().Be("Alice");
    }

    [Fact]
    public void Complete_WithWinnerNameWithWhitespace_TrimsName()
    {
        // Arrange
        var session = CreateSetupSession();
        session.Start();

        // Act
        session.Complete("  Alice  ");

        // Assert
        session.WinnerName.Should().Be("Alice");
    }

    [Fact]
    public void Complete_WithNullWinner_LeavesWinnerNull()
    {
        // Arrange
        var session = CreateSetupSession();
        session.Start();

        // Act
        session.Complete(null);

        // Assert
        session.WinnerName.Should().BeNull();
    }

    [Fact]
    public void Complete_WithTooLongWinnerName_ThrowsValidationException()
    {
        // Arrange
        var session = CreateSetupSession();
        session.Start();
        var longName = new string('a', 51);

        // Act
        var action = () => session.Complete(longName);

        // Assert
        action.Should().Throw<ValidationException>()
            .WithMessage("*Winner name cannot exceed 50 characters*");
    }

    [Fact]
    public void Complete_WhenSetup_ThrowsInvalidOperationException()
    {
        // Arrange
        var session = CreateSetupSession();

        // Act
        var action = () => session.Complete();

        // Assert
        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*Cannot complete session in Setup status*");
    }

    [Fact]
    public void Complete_WhenAlreadyCompleted_ThrowsInvalidOperationException()
    {
        // Arrange
        var session = CreateSetupSession();
        session.Start();
        session.Complete();

        // Act
        var action = () => session.Complete();

        // Assert
        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*Cannot complete session in Completed status*");
    }

    [Fact]
    public void Complete_RaisesGameSessionCompletedEvent()
    {
        // Arrange
        var session = CreateSetupSession();
        session.Start();
        session.ClearDomainEvents();

        // Act
        session.Complete();

        // Assert
        session.DomainEvents.Should().HaveCount(1);
    }

    #endregion

    #region Abandon Tests

    [Fact]
    public void Abandon_WhenSetup_TransitionsToAbandoned()
    {
        // Arrange
        var session = CreateSetupSession();

        // Act
        session.Abandon();

        // Assert
        session.Status.Should().Be(SessionStatus.Abandoned);
        session.CompletedAt.Should().NotBeNull();
        session.Status.IsFinished.Should().BeTrue();
    }

    [Fact]
    public void Abandon_WhenInProgress_TransitionsToAbandoned()
    {
        // Arrange
        var session = CreateSetupSession();
        session.Start();

        // Act
        session.Abandon();

        // Assert
        session.Status.Should().Be(SessionStatus.Abandoned);
    }

    [Fact]
    public void Abandon_WhenPaused_TransitionsToAbandoned()
    {
        // Arrange
        var session = CreateSetupSession();
        session.Start();
        session.Pause();

        // Act
        session.Abandon();

        // Assert
        session.Status.Should().Be(SessionStatus.Abandoned);
    }

    [Fact]
    public void Abandon_WithReason_AddsToNotes()
    {
        // Arrange
        var session = CreateSetupSession();
        session.Start();

        // Act
        session.Abandon("Player had to leave");

        // Assert
        session.Notes.Should().Contain("Abandoned: Player had to leave");
    }

    [Fact]
    public void Abandon_WithReasonAndExistingNotes_AppendsToNotes()
    {
        // Arrange
        var session = CreateSetupSession();
        session.Start();
        session.AddNotes("Game was going well");

        // Act
        session.Abandon("Player had to leave");

        // Assert
        session.Notes.Should().Contain("Game was going well");
        session.Notes.Should().Contain("Abandoned: Player had to leave");
    }

    [Fact]
    public void Abandon_WhenCompleted_ThrowsInvalidOperationException()
    {
        // Arrange
        var session = CreateSetupSession();
        session.Start();
        session.Complete();

        // Act
        var action = () => session.Abandon();

        // Assert
        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*Cannot abandon finished session*");
    }

    [Fact]
    public void Abandon_WhenAlreadyAbandoned_ThrowsInvalidOperationException()
    {
        // Arrange
        var session = CreateSetupSession();
        session.Abandon();

        // Act
        var action = () => session.Abandon();

        // Assert
        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*Cannot abandon finished session*");
    }

    [Fact]
    public void Abandon_RaisesGameSessionAbandonedEvent()
    {
        // Arrange
        var session = CreateSetupSession();
        session.ClearDomainEvents();

        // Act
        session.Abandon();

        // Assert
        session.DomainEvents.Should().HaveCount(1);
    }

    #endregion

    #region AddPlayer Tests

    [Fact]
    public void AddPlayer_WhenSetup_AddsPlayer()
    {
        // Arrange
        var session = CreateSetupSession();
        var newPlayer = new SessionPlayer("Charlie", 3);

        // Act
        session.AddPlayer(newPlayer);

        // Assert
        session.Players.Should().HaveCount(3);
        session.PlayerCount.Should().Be(3);
        session.HasPlayer("Charlie").Should().BeTrue();
    }

    [Fact]
    public void AddPlayer_WhenInProgress_AddsPlayer()
    {
        // Arrange
        var session = CreateSetupSession();
        session.Start();
        var newPlayer = new SessionPlayer("Charlie", 3);

        // Act
        session.AddPlayer(newPlayer);

        // Assert
        session.Players.Should().HaveCount(3);
    }

    [Fact]
    public void AddPlayer_WhenPaused_AddsPlayer()
    {
        // Arrange
        var session = CreateSetupSession();
        session.Start();
        session.Pause();
        var newPlayer = new SessionPlayer("Charlie", 3);

        // Act
        session.AddPlayer(newPlayer);

        // Assert
        session.Players.Should().HaveCount(3);
    }

    [Fact]
    public void AddPlayer_WithNullPlayer_ThrowsArgumentNullException()
    {
        // Arrange
        var session = CreateSetupSession();

        // Act
        var action = () => session.AddPlayer(null!);

        // Assert
        action.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void AddPlayer_WhenCompleted_ThrowsInvalidOperationException()
    {
        // Arrange
        var session = CreateSetupSession();
        session.Start();
        session.Complete();
        var newPlayer = new SessionPlayer("Charlie", 3);

        // Act
        var action = () => session.AddPlayer(newPlayer);

        // Assert
        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*Cannot add player to finished session*");
    }

    [Fact]
    public void AddPlayer_WhenAbandoned_ThrowsInvalidOperationException()
    {
        // Arrange
        var session = CreateSetupSession();
        session.Abandon();
        var newPlayer = new SessionPlayer("Charlie", 3);

        // Act
        var action = () => session.AddPlayer(newPlayer);

        // Assert
        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*Cannot add player to finished session*");
    }

    [Fact]
    public void AddPlayer_WhenAtMaxCapacity_ThrowsInvalidOperationException()
    {
        // Arrange
        var maxPlayers = Enumerable.Range(1, 100)
            .Select(i => new SessionPlayer($"Player{i}", i))
            .ToList();
        var session = new GameSession(Guid.NewGuid(), Guid.NewGuid(), maxPlayers);
        var newPlayer = new SessionPlayer("Extra", 1); // PlayerOrder 1-100 is valid

        // Act
        var action = () => session.AddPlayer(newPlayer);

        // Assert
        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*Session cannot have more than 100 players*");
    }

    [Fact]
    public void AddPlayer_WithDuplicateName_ThrowsInvalidOperationException()
    {
        // Arrange
        var session = CreateSetupSession();
        var duplicatePlayer = new SessionPlayer("Alice", 3);

        // Act
        var action = () => session.AddPlayer(duplicatePlayer);

        // Assert
        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*Player 'Alice' is already in this session*");
    }

    [Fact]
    public void AddPlayer_WithDuplicateNameDifferentCase_ThrowsInvalidOperationException()
    {
        // Arrange
        var session = CreateSetupSession();
        var duplicatePlayer = new SessionPlayer("ALICE", 3);

        // Act
        var action = () => session.AddPlayer(duplicatePlayer);

        // Assert
        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*Player 'ALICE' is already in this session*");
    }

    [Fact]
    public void AddPlayer_RaisesPlayerAddedToSessionEvent()
    {
        // Arrange
        var session = CreateSetupSession();
        session.ClearDomainEvents();
        var newPlayer = new SessionPlayer("Charlie", 3);

        // Act
        session.AddPlayer(newPlayer);

        // Assert
        session.DomainEvents.Should().HaveCount(1);
    }

    #endregion

    #region AddNotes Tests

    [Fact]
    public void AddNotes_WithValidNotes_AddsNotes()
    {
        // Arrange
        var session = CreateSetupSession();

        // Act
        session.AddNotes("First notes");

        // Assert
        session.Notes.Should().Be("First notes");
    }

    [Fact]
    public void AddNotes_WithExistingNotes_AppendsNotes()
    {
        // Arrange
        var session = CreateSetupSession();
        session.AddNotes("First notes");

        // Act
        session.AddNotes("Second notes");

        // Assert
        session.Notes.Should().Contain("First notes");
        session.Notes.Should().Contain("Second notes");
    }

    [Fact]
    public void AddNotes_WithEmptyString_DoesNothing()
    {
        // Arrange
        var session = CreateSetupSession();

        // Act
        session.AddNotes("");

        // Assert
        session.Notes.Should().BeNull();
    }

    [Fact]
    public void AddNotes_WithWhitespaceOnly_DoesNothing()
    {
        // Arrange
        var session = CreateSetupSession();

        // Act
        session.AddNotes("   ");

        // Assert
        session.Notes.Should().BeNull();
    }

    [Fact]
    public void AddNotes_TrimsWhitespace()
    {
        // Arrange
        var session = CreateSetupSession();

        // Act
        session.AddNotes("  Trimmed notes  ");

        // Assert
        session.Notes.Should().Be("Trimmed notes");
    }

    #endregion

    #region HasPlayer Tests

    [Fact]
    public void HasPlayer_WithExistingPlayer_ReturnsTrue()
    {
        // Arrange
        var session = CreateSetupSession();

        // Act
        var result = session.HasPlayer("Alice");

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void HasPlayer_WithNonExistentPlayer_ReturnsFalse()
    {
        // Arrange
        var session = CreateSetupSession();

        // Act
        var result = session.HasPlayer("Charlie");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void HasPlayer_IsCaseInsensitive()
    {
        // Arrange
        var session = CreateSetupSession();

        // Act & Assert
        session.HasPlayer("alice").Should().BeTrue();
        session.HasPlayer("ALICE").Should().BeTrue();
        session.HasPlayer("Alice").Should().BeTrue();
    }

    [Fact]
    public void HasPlayer_WithNullName_ReturnsFalse()
    {
        // Arrange
        var session = CreateSetupSession();

        // Act
        var result = session.HasPlayer(null!);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void HasPlayer_WithEmptyName_ReturnsFalse()
    {
        // Arrange
        var session = CreateSetupSession();

        // Act
        var result = session.HasPlayer("");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void HasPlayer_WithWhitespaceOnly_ReturnsFalse()
    {
        // Arrange
        var session = CreateSetupSession();

        // Act
        var result = session.HasPlayer("   ");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void HasPlayer_TrimsInput()
    {
        // Arrange
        var session = CreateSetupSession();

        // Act
        var result = session.HasPlayer("  Alice  ");

        // Assert
        result.Should().BeTrue();
    }

    #endregion

    #region Duration Tests

    [Fact]
    public void Duration_WhenNotCompleted_ReturnsDurationSinceStart()
    {
        // Arrange
        var session = CreateSetupSession();
        session.Start();

        // Act
        var duration = session.Duration;

        // Assert
        duration.Should().BeGreaterThanOrEqualTo(TimeSpan.Zero);
    }

    [Fact]
    public async Task Duration_WhenCompleted_ReturnsFixedDuration()
    {
        // Arrange
        var session = CreateSetupSession();
        session.Start();
        session.Complete();
        var completedDuration = session.Duration;

        // Wait a bit
        await Task.Delay(50);

        // Act
        var laterDuration = session.Duration;

        // Assert
        laterDuration.Should().Be(completedDuration);
    }

    [Fact]
    public void DurationMinutes_ReturnsNonNegativeValue()
    {
        // Arrange
        var session = CreateSetupSession();

        // Act
        var durationMinutes = session.DurationMinutes;

        // Assert
        durationMinutes.Should().BeGreaterThanOrEqualTo(0);
    }

    #endregion

    #region State Machine Complete Flow Tests

    [Fact]
    public void StateMachine_CompleteFlow_Setup_InProgress_Paused_Resume_Complete()
    {
        // Arrange
        var session = CreateSetupSession();

        // Act & Assert
        session.Status.Should().Be(SessionStatus.Setup);
        session.Status.IsActive.Should().BeTrue();
        session.Status.IsFinished.Should().BeFalse();

        session.Start();
        session.Status.Should().Be(SessionStatus.InProgress);
        session.Status.IsActive.Should().BeTrue();

        session.Pause();
        session.Status.Should().Be(SessionStatus.Paused);
        session.Status.IsActive.Should().BeTrue();

        session.Resume();
        session.Status.Should().Be(SessionStatus.InProgress);

        session.Complete("Alice");
        session.Status.Should().Be(SessionStatus.Completed);
        session.Status.IsFinished.Should().BeTrue();
        session.Status.IsActive.Should().BeFalse();
        session.WinnerName.Should().Be("Alice");
    }

    [Fact]
    public void StateMachine_AbandonFlow_FromSetup()
    {
        // Arrange
        var session = CreateSetupSession();

        // Act
        session.Abandon("Game cancelled");

        // Assert
        session.Status.Should().Be(SessionStatus.Abandoned);
        session.Status.IsFinished.Should().BeTrue();
        session.Notes.Should().Contain("Game cancelled");
    }

    [Fact]
    public void StateMachine_AbandonFlow_FromInProgress()
    {
        // Arrange
        var session = CreateSetupSession();
        session.Start();

        // Act
        session.Abandon("Player disconnected");

        // Assert
        session.Status.Should().Be(SessionStatus.Abandoned);
        session.Notes.Should().Contain("Player disconnected");
    }

    #endregion

    #region Helper Methods

    private static GameSession CreateSetupSession()
    {
        var players = new List<SessionPlayer>
        {
            new("Alice", 1),
            new("Bob", 2)
        };
        return new GameSession(Guid.NewGuid(), Guid.NewGuid(), players);
    }

    #endregion
}
