using Api.BoundedContexts.UserLibrary.Domain.Entities;
using Api.BoundedContexts.UserLibrary.Domain.Events;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace Api.Tests.BoundedContexts.UserLibrary.Domain.Entities;

/// <summary>
/// Unit tests for GameSuggestion aggregate (Admin Invitation Flow - Task 4).
/// </summary>
public sealed class GameSuggestionTests
{
    private readonly FakeTimeProvider _timeProvider =
        new(new DateTimeOffset(2026, 3, 17, 12, 0, 0, TimeSpan.Zero));

    [Fact]
    public void Create_ValidParameters_SetsAllProperties()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var adminId = Guid.NewGuid();
        var source = "invitation";

        // Act
        var suggestion = GameSuggestion.Create(userId, gameId, adminId, source, _timeProvider);

        // Assert
        Assert.NotEqual(Guid.Empty, suggestion.Id);
        Assert.Equal(userId, suggestion.UserId);
        Assert.Equal(gameId, suggestion.GameId);
        Assert.Equal(adminId, suggestion.SuggestedByUserId);
        Assert.Equal(source, suggestion.Source);
        Assert.Equal(_timeProvider.GetUtcNow().UtcDateTime, suggestion.CreatedAt);
        Assert.False(suggestion.IsDismissed);
        Assert.False(suggestion.IsAccepted);
    }

    [Fact]
    public void Create_NullSource_Allowed()
    {
        // Act
        var suggestion = GameSuggestion.Create(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), null, _timeProvider);

        // Assert
        Assert.Null(suggestion.Source);
    }

    [Fact]
    public void Create_EmptyUserId_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() =>
            GameSuggestion.Create(Guid.Empty, Guid.NewGuid(), Guid.NewGuid(), "invitation", _timeProvider));
    }

    [Fact]
    public void Create_EmptyGameId_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() =>
            GameSuggestion.Create(Guid.NewGuid(), Guid.Empty, Guid.NewGuid(), "invitation", _timeProvider));
    }

    [Fact]
    public void Create_EmptySuggestedByUserId_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() =>
            GameSuggestion.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.Empty, "invitation", _timeProvider));
    }

    [Fact]
    public void Accept_SetsIsAcceptedTrue()
    {
        // Arrange
        var suggestion = GameSuggestion.Create(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "invitation", _timeProvider);

        // Act
        suggestion.Accept();

        // Assert
        Assert.True(suggestion.IsAccepted);
    }

    [Fact]
    public void Accept_EmitsGameSuggestionAcceptedEvent()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var suggestion = GameSuggestion.Create(
            userId, gameId, Guid.NewGuid(), "invitation", _timeProvider);

        // Act
        suggestion.Accept();

        // Assert
        var domainEvent = Assert.Single(suggestion.DomainEvents);
        var acceptedEvent = Assert.IsType<GameSuggestionAcceptedEvent>(domainEvent);
        Assert.Equal(suggestion.Id, acceptedEvent.SuggestionId);
        Assert.Equal(userId, acceptedEvent.UserId);
        Assert.Equal(gameId, acceptedEvent.GameId);
    }

    [Fact]
    public void Dismiss_SetsIsDismissedTrue()
    {
        // Arrange
        var suggestion = GameSuggestion.Create(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "invitation", _timeProvider);

        // Act
        suggestion.Dismiss();

        // Assert
        Assert.True(suggestion.IsDismissed);
    }

    [Fact]
    public void Dismiss_DoesNotEmitDomainEvent()
    {
        // Arrange
        var suggestion = GameSuggestion.Create(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "invitation", _timeProvider);

        // Act
        suggestion.Dismiss();

        // Assert
        Assert.Empty(suggestion.DomainEvents);
    }
}
