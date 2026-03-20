using Api.BoundedContexts.UserLibrary.Domain.Entities;
using Api.BoundedContexts.UserLibrary.Domain.Events;
using Microsoft.Extensions.Time.Testing;
using Xunit;
using FluentAssertions;

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
        suggestion.Id.Should().NotBe(Guid.Empty);
        suggestion.UserId.Should().Be(userId);
        suggestion.GameId.Should().Be(gameId);
        suggestion.SuggestedByUserId.Should().Be(adminId);
        suggestion.Source.Should().Be(source);
        suggestion.CreatedAt.Should().Be(_timeProvider.GetUtcNow().UtcDateTime);
        suggestion.IsDismissed.Should().BeFalse();
        suggestion.IsAccepted.Should().BeFalse();
    }

    [Fact]
    public void Create_NullSource_Allowed()
    {
        // Act
        var suggestion = GameSuggestion.Create(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), null, _timeProvider);

        // Assert
        suggestion.Source.Should().BeNull();
    }

    [Fact]
    public void Create_EmptyUserId_ThrowsArgumentException()
    {
        var act = () =>
            GameSuggestion.Create(Guid.Empty, Guid.NewGuid(), Guid.NewGuid(), "invitation", _timeProvider);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_EmptyGameId_ThrowsArgumentException()
    {
        var act2 = () =>
            GameSuggestion.Create(Guid.NewGuid(), Guid.Empty, Guid.NewGuid(), "invitation", _timeProvider);
        act2.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_EmptySuggestedByUserId_ThrowsArgumentException()
    {
        var act3 = () =>
            GameSuggestion.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.Empty, "invitation", _timeProvider);
        act3.Should().Throw<ArgumentException>();
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
        suggestion.IsAccepted.Should().BeTrue();
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
        var domainEvent = suggestion.DomainEvents.Should().ContainSingle().Subject;
        var acceptedEvent = domainEvent.Should().BeOfType<GameSuggestionAcceptedEvent>().Subject;
        acceptedEvent.SuggestionId.Should().Be(suggestion.Id);
        acceptedEvent.UserId.Should().Be(userId);
        acceptedEvent.GameId.Should().Be(gameId);
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
        suggestion.IsDismissed.Should().BeTrue();
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
        suggestion.DomainEvents.Should().BeEmpty();
    }
}
