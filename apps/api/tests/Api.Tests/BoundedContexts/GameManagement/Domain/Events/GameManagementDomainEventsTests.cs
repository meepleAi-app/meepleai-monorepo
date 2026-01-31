using Api.BoundedContexts.GameManagement.Domain.Events;
using Api.BoundedContexts.GameManagement.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.GameManagement.Domain.Events;

/// <summary>
/// Tests for GameManagement domain events.
/// Issue #3025: Backend 90% Coverage Target - Phase 21 PR#2
/// </summary>
[Trait("Category", "Unit")]
public sealed class GameManagementDomainEventsTests
{
    #region GameCreatedEvent Tests

    [Fact]
    public void GameCreatedEvent_WithAllParameters_SetsProperties()
    {
        // Arrange
        var gameId = Guid.NewGuid();

        // Act
        var evt = new GameCreatedEvent(gameId, "Catan", 13);

        // Assert
        evt.GameId.Should().Be(gameId);
        evt.Name.Should().Be("Catan");
        evt.BggId.Should().Be(13);
    }

    [Fact]
    public void GameCreatedEvent_WithoutBggId_SetsNullBggId()
    {
        // Arrange
        var gameId = Guid.NewGuid();

        // Act
        var evt = new GameCreatedEvent(gameId, "Custom Game");

        // Assert
        evt.GameId.Should().Be(gameId);
        evt.Name.Should().Be("Custom Game");
        evt.BggId.Should().BeNull();
    }

    #endregion

    #region GameUpdatedEvent Tests

    [Fact]
    public void GameUpdatedEvent_SetsProperties()
    {
        // Arrange
        var gameId = Guid.NewGuid();

        // Act
        var evt = new GameUpdatedEvent(gameId, "Updated Game Name");

        // Assert
        evt.GameId.Should().Be(gameId);
        evt.Name.Should().Be("Updated Game Name");
    }

    #endregion

    #region GameLinkedToBggEvent Tests

    [Fact]
    public void GameLinkedToBggEvent_SetsProperties()
    {
        // Arrange
        var gameId = Guid.NewGuid();

        // Act
        var evt = new GameLinkedToBggEvent(gameId, 174430);

        // Assert
        evt.GameId.Should().Be(gameId);
        evt.BggId.Should().Be(174430);
    }

    #endregion

    #region GameSessionCreatedEvent Tests

    [Fact]
    public void GameSessionCreatedEvent_SetsProperties()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var gameId = Guid.NewGuid();

        // Act
        var evt = new GameSessionCreatedEvent(sessionId, gameId, 4);

        // Assert
        evt.SessionId.Should().Be(sessionId);
        evt.GameId.Should().Be(gameId);
        evt.PlayerCount.Should().Be(4);
    }

    [Fact]
    public void GameSessionCreatedEvent_WithSinglePlayer_SetsCorrectCount()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var gameId = Guid.NewGuid();

        // Act
        var evt = new GameSessionCreatedEvent(sessionId, gameId, 1);

        // Assert
        evt.PlayerCount.Should().Be(1);
    }

    [Fact]
    public void GameSessionCreatedEvent_WithMaxPlayers_SetsCorrectCount()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var gameId = Guid.NewGuid();

        // Act
        var evt = new GameSessionCreatedEvent(sessionId, gameId, 8);

        // Assert
        evt.PlayerCount.Should().Be(8);
    }

    #endregion

    #region GameSessionStartedEvent Tests

    [Fact]
    public void GameSessionStartedEvent_SetsProperties()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var startedAt = DateTime.UtcNow;

        // Act
        var evt = new GameSessionStartedEvent(sessionId, gameId, startedAt);

        // Assert
        evt.SessionId.Should().Be(sessionId);
        evt.GameId.Should().Be(gameId);
        evt.StartedAt.Should().Be(startedAt);
    }

    #endregion

    #region GameSessionPausedEvent Tests

    [Fact]
    public void GameSessionPausedEvent_SetsProperties()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var pausedAt = DateTime.UtcNow;

        // Act
        var evt = new GameSessionPausedEvent(sessionId, pausedAt);

        // Assert
        evt.SessionId.Should().Be(sessionId);
        evt.PausedAt.Should().Be(pausedAt);
    }

    #endregion

    #region GameSessionResumedEvent Tests

    [Fact]
    public void GameSessionResumedEvent_SetsProperties()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var resumedAt = DateTime.UtcNow;

        // Act
        var evt = new GameSessionResumedEvent(sessionId, resumedAt);

        // Assert
        evt.SessionId.Should().Be(sessionId);
        evt.ResumedAt.Should().Be(resumedAt);
    }

    #endregion

    #region GameSessionCompletedEvent Tests

    [Fact]
    public void GameSessionCompletedEvent_SetsProperties()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var completedAt = DateTime.UtcNow;
        var duration = TimeSpan.FromMinutes(90);

        // Act
        var evt = new GameSessionCompletedEvent(sessionId, gameId, completedAt, duration);

        // Assert
        evt.SessionId.Should().Be(sessionId);
        evt.GameId.Should().Be(gameId);
        evt.CompletedAt.Should().Be(completedAt);
        evt.Duration.Should().Be(duration);
    }

    [Fact]
    public void GameSessionCompletedEvent_WithShortDuration_SetsCorrectDuration()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var completedAt = DateTime.UtcNow;
        var duration = TimeSpan.FromMinutes(15);

        // Act
        var evt = new GameSessionCompletedEvent(sessionId, gameId, completedAt, duration);

        // Assert
        evt.Duration.Should().Be(duration);
    }

    [Fact]
    public void GameSessionCompletedEvent_WithLongDuration_SetsCorrectDuration()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var completedAt = DateTime.UtcNow;
        var duration = TimeSpan.FromHours(5);

        // Act
        var evt = new GameSessionCompletedEvent(sessionId, gameId, completedAt, duration);

        // Assert
        evt.Duration.Should().Be(duration);
    }

    #endregion

    #region GameSessionAbandonedEvent Tests

    [Fact]
    public void GameSessionAbandonedEvent_WithReason_SetsProperties()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var abandonedAt = DateTime.UtcNow;

        // Act
        var evt = new GameSessionAbandonedEvent(sessionId, abandonedAt, "Player left");

        // Assert
        evt.SessionId.Should().Be(sessionId);
        evt.AbandonedAt.Should().Be(abandonedAt);
        evt.Reason.Should().Be("Player left");
    }

    [Fact]
    public void GameSessionAbandonedEvent_WithoutReason_SetsNullReason()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var abandonedAt = DateTime.UtcNow;

        // Act
        var evt = new GameSessionAbandonedEvent(sessionId, abandonedAt);

        // Assert
        evt.SessionId.Should().Be(sessionId);
        evt.AbandonedAt.Should().Be(abandonedAt);
        evt.Reason.Should().BeNull();
    }

    #endregion

    #region PlayerAddedToSessionEvent Tests

    [Fact]
    public void PlayerAddedToSessionEvent_SetsProperties()
    {
        // Arrange
        var sessionId = Guid.NewGuid();

        // Act
        var evt = new PlayerAddedToSessionEvent(sessionId, "Alice", 1);

        // Assert
        evt.SessionId.Should().Be(sessionId);
        evt.PlayerName.Should().Be("Alice");
        evt.PlayerNumber.Should().Be(1);
    }

    [Fact]
    public void PlayerAddedToSessionEvent_WithHigherPlayerNumber_SetsCorrectNumber()
    {
        // Arrange
        var sessionId = Guid.NewGuid();

        // Act
        var evt = new PlayerAddedToSessionEvent(sessionId, "Bob", 4);

        // Assert
        evt.PlayerNumber.Should().Be(4);
    }

    #endregion

    #region GameSessionStateCreatedEvent Tests

    [Fact]
    public void GameSessionStateCreatedEvent_SetsProperties()
    {
        // Arrange
        var stateId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();

        // Act
        var evt = new GameSessionStateCreatedEvent(stateId, sessionId, 4);

        // Assert
        evt.StateId.Should().Be(stateId);
        evt.SessionId.Should().Be(sessionId);
        evt.PlayerCount.Should().Be(4);
    }

    [Fact]
    public void GameSessionStateCreatedEvent_WithMinPlayers_SetsCorrectCount()
    {
        // Arrange
        var stateId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();

        // Act
        var evt = new GameSessionStateCreatedEvent(stateId, sessionId, 1);

        // Assert
        evt.PlayerCount.Should().Be(1);
    }

    #endregion

    #region GameStateInitializedEvent Tests

    [Fact]
    public void GameStateInitializedEvent_SetsProperties()
    {
        // Arrange
        var stateId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var templateId = Guid.NewGuid();

        // Act
        var evt = new GameStateInitializedEvent(stateId, sessionId, templateId);

        // Assert
        evt.StateId.Should().Be(stateId);
        evt.GameSessionId.Should().Be(sessionId);
        evt.TemplateId.Should().Be(templateId);
    }

    #endregion

    #region GameStateUpdatedEvent Tests

    [Fact]
    public void GameStateUpdatedEvent_SetsProperties()
    {
        // Arrange
        var stateId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var updatedAt = DateTime.UtcNow;

        // Act
        var evt = new GameStateUpdatedEvent(stateId, sessionId, 5, updatedAt);

        // Assert
        evt.StateId.Should().Be(stateId);
        evt.GameSessionId.Should().Be(sessionId);
        evt.NewVersion.Should().Be(5);
        evt.UpdatedAt.Should().Be(updatedAt);
    }

    [Fact]
    public void GameStateUpdatedEvent_WithFirstVersion_SetsCorrectVersion()
    {
        // Arrange
        var stateId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var updatedAt = DateTime.UtcNow;

        // Act
        var evt = new GameStateUpdatedEvent(stateId, sessionId, 1, updatedAt);

        // Assert
        evt.NewVersion.Should().Be(1);
    }

    #endregion

    #region GameStateSnapshotCreatedEvent Tests

    [Fact]
    public void GameStateSnapshotCreatedEvent_SetsProperties()
    {
        // Arrange
        var stateId = Guid.NewGuid();
        var snapshotId = Guid.NewGuid();

        // Act
        var evt = new GameStateSnapshotCreatedEvent(stateId, snapshotId, 10);

        // Assert
        evt.StateId.Should().Be(stateId);
        evt.SnapshotId.Should().Be(snapshotId);
        evt.TurnNumber.Should().Be(10);
    }

    [Fact]
    public void GameStateSnapshotCreatedEvent_WithInitialTurn_SetsCorrectTurnNumber()
    {
        // Arrange
        var stateId = Guid.NewGuid();
        var snapshotId = Guid.NewGuid();

        // Act
        var evt = new GameStateSnapshotCreatedEvent(stateId, snapshotId, 0);

        // Assert
        evt.TurnNumber.Should().Be(0);
    }

    #endregion

    #region GameStateRestoredEvent Tests

    [Fact]
    public void GameStateRestoredEvent_SetsProperties()
    {
        // Arrange
        var stateId = Guid.NewGuid();
        var snapshotId = Guid.NewGuid();

        // Act
        var evt = new GameStateRestoredEvent(stateId, snapshotId, 7);

        // Assert
        evt.StateId.Should().Be(stateId);
        evt.SnapshotId.Should().Be(snapshotId);
        evt.TurnNumber.Should().Be(7);
    }

    [Fact]
    public void GameStateRestoredEvent_WithFirstTurn_SetsCorrectTurnNumber()
    {
        // Arrange
        var stateId = Guid.NewGuid();
        var snapshotId = Guid.NewGuid();

        // Act
        var evt = new GameStateRestoredEvent(stateId, snapshotId, 1);

        // Assert
        evt.TurnNumber.Should().Be(1);
    }

    #endregion

    #region GamePhaseChangedEvent Tests

    [Fact]
    public void GamePhaseChangedEvent_SetsProperties()
    {
        // Arrange
        var stateId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var oldPhase = GamePhase.Setup;
        var newPhase = GamePhase.InProgress;

        // Act
        var evt = new GamePhaseChangedEvent(stateId, sessionId, oldPhase, newPhase);

        // Assert
        evt.StateId.Should().Be(stateId);
        evt.SessionId.Should().Be(sessionId);
        evt.OldPhase.Should().Be(GamePhase.Setup);
        evt.NewPhase.Should().Be(GamePhase.InProgress);
    }

    [Fact]
    public void GamePhaseChangedEvent_FromInProgressToScoring_SetsCorrectPhases()
    {
        // Arrange
        var stateId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();

        // Act
        var evt = new GamePhaseChangedEvent(
            stateId,
            sessionId,
            GamePhase.InProgress,
            GamePhase.Scoring);

        // Assert
        evt.OldPhase.Should().Be(GamePhase.InProgress);
        evt.NewPhase.Should().Be(GamePhase.Scoring);
    }

    [Fact]
    public void GamePhaseChangedEvent_FromScoringToCompleted_SetsCorrectPhases()
    {
        // Arrange
        var stateId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();

        // Act
        var evt = new GamePhaseChangedEvent(
            stateId,
            sessionId,
            GamePhase.Scoring,
            GamePhase.Completed);

        // Assert
        evt.OldPhase.Should().Be(GamePhase.Scoring);
        evt.NewPhase.Should().Be(GamePhase.Completed);
    }

    [Fact]
    public void GamePhaseChangedEvent_ToCustomPhase_SetsCorrectPhases()
    {
        // Arrange
        var stateId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();

        // Act
        var evt = new GamePhaseChangedEvent(
            stateId,
            sessionId,
            GamePhase.InProgress,
            GamePhase.Custom);

        // Assert
        evt.OldPhase.Should().Be(GamePhase.InProgress);
        evt.NewPhase.Should().Be(GamePhase.Custom);
    }

    #endregion

    #region TurnAdvancedEvent Tests

    [Fact]
    public void TurnAdvancedEvent_WithPreviousPlayer_SetsProperties()
    {
        // Arrange
        var stateId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();

        // Act
        var evt = new TurnAdvancedEvent(stateId, sessionId, "Alice", "Bob");

        // Assert
        evt.StateId.Should().Be(stateId);
        evt.SessionId.Should().Be(sessionId);
        evt.PreviousPlayer.Should().Be("Alice");
        evt.CurrentPlayer.Should().Be("Bob");
    }

    [Fact]
    public void TurnAdvancedEvent_WithoutPreviousPlayer_SetsNullPreviousPlayer()
    {
        // Arrange
        var stateId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();

        // Act
        var evt = new TurnAdvancedEvent(stateId, sessionId, null, "Alice");

        // Assert
        evt.StateId.Should().Be(stateId);
        evt.SessionId.Should().Be(sessionId);
        evt.PreviousPlayer.Should().BeNull();
        evt.CurrentPlayer.Should().Be("Alice");
    }

    [Fact]
    public void TurnAdvancedEvent_BetweenPlayers_SetsCorrectValues()
    {
        // Arrange
        var stateId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();

        // Act
        var evt = new TurnAdvancedEvent(stateId, sessionId, "Charlie", "Diana");

        // Assert
        evt.PreviousPlayer.Should().Be("Charlie");
        evt.CurrentPlayer.Should().Be("Diana");
    }

    #endregion

    #region RoundAdvancedEvent Tests

    [Fact]
    public void RoundAdvancedEvent_SetsProperties()
    {
        // Arrange
        var stateId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();

        // Act
        var evt = new RoundAdvancedEvent(stateId, sessionId, 2, 3);

        // Assert
        evt.StateId.Should().Be(stateId);
        evt.SessionId.Should().Be(sessionId);
        evt.OldRound.Should().Be(2);
        evt.NewRound.Should().Be(3);
    }

    [Fact]
    public void RoundAdvancedEvent_FromInitial_SetsCorrectValues()
    {
        // Arrange
        var stateId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();

        // Act
        var evt = new RoundAdvancedEvent(stateId, sessionId, 0, 1);

        // Assert
        evt.OldRound.Should().Be(0);
        evt.NewRound.Should().Be(1);
    }

    [Fact]
    public void RoundAdvancedEvent_ToHigherRound_SetsCorrectValues()
    {
        // Arrange
        var stateId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();

        // Act
        var evt = new RoundAdvancedEvent(stateId, sessionId, 5, 6);

        // Assert
        evt.OldRound.Should().Be(5);
        evt.NewRound.Should().Be(6);
    }

    #endregion

    #region PlayerScoreUpdatedEvent Tests

    [Fact]
    public void PlayerScoreUpdatedEvent_SetsProperties()
    {
        // Arrange
        var stateId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();

        // Act
        var evt = new PlayerScoreUpdatedEvent(stateId, sessionId, "Alice", 10, 15);

        // Assert
        evt.StateId.Should().Be(stateId);
        evt.SessionId.Should().Be(sessionId);
        evt.PlayerName.Should().Be("Alice");
        evt.OldScore.Should().Be(10);
        evt.NewScore.Should().Be(15);
    }

    [Fact]
    public void PlayerScoreUpdatedEvent_WithScoreDecrease_SetsCorrectValues()
    {
        // Arrange
        var stateId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();

        // Act
        var evt = new PlayerScoreUpdatedEvent(stateId, sessionId, "Bob", 20, 18);

        // Assert
        evt.OldScore.Should().Be(20);
        evt.NewScore.Should().Be(18);
    }

    [Fact]
    public void PlayerScoreUpdatedEvent_FromZero_SetsCorrectValues()
    {
        // Arrange
        var stateId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();

        // Act
        var evt = new PlayerScoreUpdatedEvent(stateId, sessionId, "Charlie", 0, 7);

        // Assert
        evt.OldScore.Should().Be(0);
        evt.NewScore.Should().Be(7);
    }

    #endregion

    #region PlayerResourceUpdatedEvent Tests

    [Fact]
    public void PlayerResourceUpdatedEvent_SetsProperties()
    {
        // Arrange
        var stateId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();

        // Act
        var evt = new PlayerResourceUpdatedEvent(stateId, sessionId, "Bob", "gold", 5, 8);

        // Assert
        evt.StateId.Should().Be(stateId);
        evt.SessionId.Should().Be(sessionId);
        evt.PlayerName.Should().Be("Bob");
        evt.ResourceName.Should().Be("gold");
        evt.OldValue.Should().Be(5);
        evt.NewValue.Should().Be(8);
    }

    [Fact]
    public void PlayerResourceUpdatedEvent_WithResourceDecrease_SetsCorrectValues()
    {
        // Arrange
        var stateId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();

        // Act
        var evt = new PlayerResourceUpdatedEvent(stateId, sessionId, "Alice", "wood", 10, 3);

        // Assert
        evt.OldValue.Should().Be(10);
        evt.NewValue.Should().Be(3);
    }

    [Fact]
    public void PlayerResourceUpdatedEvent_FromZero_SetsCorrectValues()
    {
        // Arrange
        var stateId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();

        // Act
        var evt = new PlayerResourceUpdatedEvent(stateId, sessionId, "Charlie", "stone", 0, 5);

        // Assert
        evt.OldValue.Should().Be(0);
        evt.NewValue.Should().Be(5);
    }

    #endregion
}
