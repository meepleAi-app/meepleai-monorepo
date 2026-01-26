using Api.BoundedContexts.UserLibrary.Domain.Events;
using Api.BoundedContexts.UserLibrary.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.UserLibrary.Domain.Events;

/// <summary>
/// Tests for UserLibrary domain events.
/// Issue #3025: Backend 90% Coverage Target - Phase 24
/// </summary>
[Trait("Category", "Unit")]
public sealed class UserLibraryDomainEventsTests
{
    #region GameAddedToLibraryEvent Tests

    [Fact]
    public void GameAddedToLibraryEvent_SetsProperties()
    {
        // Arrange
        var entryId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var gameId = Guid.NewGuid();

        // Act
        var evt = new GameAddedToLibraryEvent(entryId, userId, gameId);

        // Assert
        evt.EntryId.Should().Be(entryId);
        evt.UserId.Should().Be(userId);
        evt.GameId.Should().Be(gameId);
    }

    #endregion

    #region GameRemovedFromLibraryEvent Tests

    [Fact]
    public void GameRemovedFromLibraryEvent_SetsProperties()
    {
        // Arrange
        var entryId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var gameId = Guid.NewGuid();

        // Act
        var evt = new GameRemovedFromLibraryEvent(entryId, userId, gameId);

        // Assert
        evt.EntryId.Should().Be(entryId);
        evt.UserId.Should().Be(userId);
        evt.GameId.Should().Be(gameId);
    }

    #endregion

    #region GameSessionRecordedEvent Tests

    [Fact]
    public void GameSessionRecordedEvent_SetsAllProperties()
    {
        // Arrange
        var libraryEntryId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var playedAt = DateTime.UtcNow.AddHours(-2);
        var occurredAt = DateTime.UtcNow;

        // Act
        var evt = new GameSessionRecordedEvent(
            libraryEntryId,
            userId,
            gameId,
            sessionId,
            playedAt,
            durationMinutes: 90,
            didWin: true,
            occurredAt);

        // Assert
        evt.LibraryEntryId.Should().Be(libraryEntryId);
        evt.UserId.Should().Be(userId);
        evt.GameId.Should().Be(gameId);
        evt.SessionId.Should().Be(sessionId);
        evt.PlayedAt.Should().Be(playedAt);
        evt.DurationMinutes.Should().Be(90);
        evt.DidWin.Should().BeTrue();
    }

    [Fact]
    public void GameSessionRecordedEvent_WithNoWinner_SetsNullDidWin()
    {
        // Arrange
        var evt = new GameSessionRecordedEvent(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            DateTime.UtcNow,
            durationMinutes: 60,
            didWin: null,
            DateTime.UtcNow);

        // Assert
        evt.DidWin.Should().BeNull();
    }

    [Fact]
    public void GameSessionRecordedEvent_WithLoss_SetsFalseDidWin()
    {
        // Arrange
        var evt = new GameSessionRecordedEvent(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            DateTime.UtcNow,
            durationMinutes: 45,
            didWin: false,
            DateTime.UtcNow);

        // Assert
        evt.DidWin.Should().BeFalse();
    }

    #endregion

    #region GameStateChangedEvent Tests

    [Fact]
    public void GameStateChangedEvent_SetsAllProperties()
    {
        // Arrange
        var libraryEntryId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var occurredAt = DateTime.UtcNow;

        // Act
        var evt = new GameStateChangedEvent(
            libraryEntryId,
            userId,
            gameId,
            previousState: GameStateType.Nuovo,
            newState: GameStateType.Owned,
            occurredAt);

        // Assert
        evt.LibraryEntryId.Should().Be(libraryEntryId);
        evt.UserId.Should().Be(userId);
        evt.GameId.Should().Be(gameId);
        evt.PreviousState.Should().Be(GameStateType.Nuovo);
        evt.NewState.Should().Be(GameStateType.Owned);
    }

    [Fact]
    public void GameStateChangedEvent_WithNullPreviousState_SetsNullPrevious()
    {
        // Arrange
        var evt = new GameStateChangedEvent(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            previousState: null,
            newState: GameStateType.Wishlist,
            DateTime.UtcNow);

        // Assert
        evt.PreviousState.Should().BeNull();
        evt.NewState.Should().Be(GameStateType.Wishlist);
    }

    [Fact]
    public void GameStateChangedEvent_FromOwnedToInPrestito_SetsCorrectStates()
    {
        // Act
        var evt = new GameStateChangedEvent(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            previousState: GameStateType.Owned,
            newState: GameStateType.InPrestito,
            DateTime.UtcNow);

        // Assert
        evt.PreviousState.Should().Be(GameStateType.Owned);
        evt.NewState.Should().Be(GameStateType.InPrestito);
    }

    [Fact]
    public void GameStateChangedEvent_FromInPrestitoToOwned_SetsCorrectStates()
    {
        // Act
        var evt = new GameStateChangedEvent(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            previousState: GameStateType.InPrestito,
            newState: GameStateType.Owned,
            DateTime.UtcNow);

        // Assert
        evt.PreviousState.Should().Be(GameStateType.InPrestito);
        evt.NewState.Should().Be(GameStateType.Owned);
    }

    #endregion
}
