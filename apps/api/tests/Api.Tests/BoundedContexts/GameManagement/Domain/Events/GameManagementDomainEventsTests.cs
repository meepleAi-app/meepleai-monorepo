using Api.BoundedContexts.GameManagement.Domain.Events;
using Api.BoundedContexts.GameManagement.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.GameManagement.Domain.Events;

/// <summary>
/// Tests for GameManagement domain events.
/// Issue #3025: Backend 90% Coverage Target - Phase 22
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

    #endregion
}
