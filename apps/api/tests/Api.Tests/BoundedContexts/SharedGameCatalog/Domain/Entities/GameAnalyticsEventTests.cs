using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;
using Api.BoundedContexts.SharedGameCatalog.Domain.Enums;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Domain.Entities;

[Trait("Category", TestCategories.Unit)]
public class GameAnalyticsEventTests
{
    [Fact]
    public void Record_WithValidParameters_CreatesEvent()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        // Act
        var evt = GameAnalyticsEvent.Record(gameId, GameEventType.View, userId);

        // Assert
        evt.Should().NotBeNull();
        evt.Id.Should().NotBe(Guid.Empty);
        evt.GameId.Should().Be(gameId);
        evt.EventType.Should().Be(GameEventType.View);
        evt.UserId.Should().Be(userId);
        evt.Timestamp.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void Record_WithNullUserId_CreatesAnonymousEvent()
    {
        // Arrange
        var gameId = Guid.NewGuid();

        // Act
        var evt = GameAnalyticsEvent.Record(gameId, GameEventType.Search);

        // Assert
        evt.Should().NotBeNull();
        evt.UserId.Should().BeNull();
        evt.EventType.Should().Be(GameEventType.Search);
    }

    [Fact]
    public void Record_WithEmptyGameId_ThrowsArgumentException()
    {
        // Act
        var act = () => GameAnalyticsEvent.Record(Guid.Empty, GameEventType.View);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("gameId");
    }

    [Fact]
    public void Record_WithInvalidEventType_ThrowsArgumentException()
    {
        // Act
        var act = () => GameAnalyticsEvent.Record(Guid.NewGuid(), (GameEventType)99);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("eventType");
    }

    [Theory]
    [InlineData(GameEventType.Search, 3)]
    [InlineData(GameEventType.View, 1)]
    [InlineData(GameEventType.LibraryAdd, 5)]
    [InlineData(GameEventType.Play, 10)]
    public void GetEventWeight_ReturnsCorrectWeight(GameEventType eventType, int expectedWeight)
    {
        // Arrange
        var evt = GameAnalyticsEvent.Record(Guid.NewGuid(), eventType);

        // Act
        var weight = evt.GetEventWeight();

        // Assert
        weight.Should().Be(expectedWeight);
    }

    [Fact]
    public void CalculateDecayedScore_ForRecentEvent_ReturnsHighScore()
    {
        // Arrange - Play event (+10 weight), just created (0 days ago)
        var evt = GameAnalyticsEvent.Record(Guid.NewGuid(), GameEventType.Play);

        // Act
        var score = evt.CalculateDecayedScore();

        // Assert - exp(0) = 1, so score should be close to 10
        score.Should().BeApproximately(10.0, 0.5);
    }

    [Fact]
    public void CalculateDecayedScore_ForOldEvent_ReturnsLowerScore()
    {
        // Arrange - Play event from 7 days ago
        var evt = new GameAnalyticsEvent(
            Guid.NewGuid(),
            Guid.NewGuid(),
            GameEventType.Play,
            null,
            DateTime.UtcNow.AddDays(-7));

        // Act
        var score = evt.CalculateDecayedScore();

        // Assert - exp(-1) ≈ 0.368, so score ≈ 10 * 0.368 ≈ 3.68
        score.Should().BeApproximately(3.68, 0.5);
    }

    [Theory]
    [InlineData(GameEventType.Search)]
    [InlineData(GameEventType.View)]
    [InlineData(GameEventType.LibraryAdd)]
    [InlineData(GameEventType.Play)]
    public void Record_AllEventTypes_CreatesSuccessfully(GameEventType eventType)
    {
        // Act
        var evt = GameAnalyticsEvent.Record(Guid.NewGuid(), eventType);

        // Assert
        evt.Should().NotBeNull();
        evt.EventType.Should().Be(eventType);
    }

    [Fact]
    public void Constructor_Internal_ReconstitutesCorrectly()
    {
        // Arrange
        var id = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var timestamp = new DateTime(2026, 2, 1, 12, 0, 0, DateTimeKind.Utc);

        // Act
        var evt = new GameAnalyticsEvent(id, gameId, GameEventType.LibraryAdd, userId, timestamp);

        // Assert
        evt.Id.Should().Be(id);
        evt.GameId.Should().Be(gameId);
        evt.EventType.Should().Be(GameEventType.LibraryAdd);
        evt.UserId.Should().Be(userId);
        evt.Timestamp.Should().Be(timestamp);
    }
}
