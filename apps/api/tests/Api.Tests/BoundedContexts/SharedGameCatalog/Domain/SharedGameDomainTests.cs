using Api.BoundedContexts.SharedGameCatalog.Domain.Aggregates;
using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;
using Api.BoundedContexts.SharedGameCatalog.Domain.Events;
using Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;
using Api.Tests.Constants;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Domain;

[Trait("Category", TestCategories.Unit)]
public class SharedGameDomainTests
{
    private static readonly Guid TestUserId = Guid.NewGuid();

    [Fact]
    public void Create_WithValidData_CreatesSharedGameSuccessfully()
    {
        // Arrange & Act
        var game = SharedGame.Create(
            title: "Catan",
            yearPublished: 1995,
            description: "A classic resource management game",
            minPlayers: 3,
            maxPlayers: 4,
            playingTimeMinutes: 90,
            minAge: 10,
            complexityRating: 2.5m,
            averageRating: 7.8m,
            imageUrl: "https://example.com/catan.jpg",
            thumbnailUrl: "https://example.com/catan-thumb.jpg",
            rules: GameRules.Create("Game rules content", "en"),
            createdBy: TestUserId,
            bggId: 13);

        // Assert
        Assert.NotEqual(Guid.Empty, game.Id);
        Assert.Equal("Catan", game.Title);
        Assert.Equal(1995, game.YearPublished);
        Assert.Equal(3, game.MinPlayers);
        Assert.Equal(4, game.MaxPlayers);
        Assert.Equal(90, game.PlayingTimeMinutes);
        Assert.Equal(10, game.MinAge);
        Assert.Equal(2.5m, game.ComplexityRating);
        Assert.Equal(7.8m, game.AverageRating);
        Assert.Equal(13, game.BggId);
        Assert.Equal(GameStatus.Draft, game.Status);
        Assert.Equal(TestUserId, game.CreatedBy);
        Assert.NotNull(game.Rules);
        Assert.Equal("Game rules content", game.Rules.Content);
        Assert.Equal("en", game.Rules.Language);

        // Assert domain event raised
        Assert.Single(game.DomainEvents);
        var createdEvent = Assert.IsType<SharedGameCreatedEvent>(game.DomainEvents.First());
        Assert.Equal(game.Id, createdEvent.GameId);
        Assert.Equal("Catan", createdEvent.Title);
        Assert.Equal(TestUserId, createdEvent.CreatedBy);
    }

    [Fact]
    public void UpdateInfo_WithValidData_UpdatesGameSuccessfully()
    {
        // Arrange
        var game = CreateValidGame();
        var modifierId = Guid.NewGuid();

        // Act
        game.UpdateInfo(
            title: "Settlers of Catan",
            yearPublished: 1995,
            description: "Updated description",
            minPlayers: 2,
            maxPlayers: 6,
            playingTimeMinutes: 120,
            minAge: 12,
            complexityRating: 3.0m,
            averageRating: 8.0m,
            imageUrl: "https://example.com/new.jpg",
            thumbnailUrl: "https://example.com/new-thumb.jpg",
            rules: GameRules.Create("Updated rules", "it"),
            modifiedBy: modifierId);

        // Assert
        Assert.Equal("Settlers of Catan", game.Title);
        Assert.Equal(2, game.MinPlayers);
        Assert.Equal(6, game.MaxPlayers);
        Assert.Equal(120, game.PlayingTimeMinutes);
        Assert.Equal(12, game.MinAge);
        Assert.Equal(3.0m, game.ComplexityRating);
        Assert.Equal(8.0m, game.AverageRating);
        Assert.Equal(modifierId, game.ModifiedBy);
        Assert.NotNull(game.ModifiedAt);
        Assert.NotNull(game.Rules);
        Assert.Equal("Updated rules", game.Rules.Content);
        Assert.Equal("it", game.Rules.Language);

        // Assert domain event raised (CreatedEvent + UpdatedEvent)
        Assert.Equal(2, game.DomainEvents.Count);
        var updatedEvent = Assert.IsType<SharedGameUpdatedEvent>(game.DomainEvents.Last());
        Assert.Equal(game.Id, updatedEvent.GameId);
        Assert.Equal(modifierId, updatedEvent.ModifiedBy);
    }

    [Theory]
    [InlineData("", 1995, "Description", 1, 4, 90, 10)] // Empty title
    [InlineData("Catan", 1800, "Description", 1, 4, 90, 10)] // Year too old
    [InlineData("Catan", 2030, "Description", 1, 4, 90, 10)] // Year too far future
    [InlineData("Catan", 1995, "", 1, 4, 90, 10)] // Empty description
    [InlineData("Catan", 1995, "Description", 0, 4, 90, 10)] // MinPlayers = 0
    [InlineData("Catan", 1995, "Description", 5, 2, 90, 10)] // MaxPlayers < MinPlayers
    [InlineData("Catan", 1995, "Description", 1, 4, 0, 10)] // PlayingTime = 0
    [InlineData("Catan", 1995, "Description", 1, 4, 90, -1)] // MinAge negative
    public void Create_WithInvalidData_ThrowsArgumentException(
        string title,
        int year,
        string description,
        int minPlayers,
        int maxPlayers,
        int playingTime,
        int minAge)
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() =>
            SharedGame.Create(
                title,
                year,
                description,
                minPlayers,
                maxPlayers,
                playingTime,
                minAge,
                null,
                null,
                "https://example.com/image.jpg",
                "https://example.com/thumb.jpg",
                null,
                TestUserId,
                null));
    }

    [Theory]
    [InlineData(0.5)] // Below 1.0
    [InlineData(5.5)] // Above 5.0
    public void Create_WithInvalidComplexityRating_ThrowsArgumentException(decimal rating)
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() =>
            SharedGame.Create(
                "Catan",
                1995,
                "Description",
                3,
                4,
                90,
                10,
                rating,
                null,
                "https://example.com/image.jpg",
                "https://example.com/thumb.jpg",
                null,
                TestUserId,
                null));
    }

    [Theory]
    [InlineData(0.5)] // Below 1.0
    [InlineData(10.5)] // Above 10.0
    public void Create_WithInvalidAverageRating_ThrowsArgumentException(decimal rating)
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() =>
            SharedGame.Create(
                "Catan",
                1995,
                "Description",
                3,
                4,
                90,
                10,
                null,
                rating,
                "https://example.com/image.jpg",
                "https://example.com/thumb.jpg",
                null,
                TestUserId,
                null));
    }

    [Theory]
    [InlineData("not-a-url")]
    [InlineData("relative/path.jpg")]
    [InlineData("")]
    public void Create_WithInvalidImageUrl_ThrowsArgumentException(string url)
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() =>
            SharedGame.Create(
                "Catan",
                1995,
                "Description",
                3,
                4,
                90,
                10,
                null,
                null,
                url,
                "https://example.com/thumb.jpg",
                null,
                TestUserId,
                null));
    }

    [Fact]
    public void Create_WithEmptyCreatedBy_ThrowsArgumentException()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() =>
            SharedGame.Create(
                "Catan",
                1995,
                "Description",
                3,
                4,
                90,
                10,
                null,
                null,
                "https://example.com/image.jpg",
                "https://example.com/thumb.jpg",
                null,
                Guid.Empty,
                null));
    }

    private static SharedGame CreateValidGame()
    {
        return SharedGame.Create(
            "Test Game",
            2020,
            "Test description",
            2,
            4,
            60,
            8,
            2.0m,
            7.0m,
            "https://example.com/test.jpg",
            "https://example.com/test-thumb.jpg",
            GameRules.Create("Test rules", "en"),
            TestUserId,
            null);
    }
}
