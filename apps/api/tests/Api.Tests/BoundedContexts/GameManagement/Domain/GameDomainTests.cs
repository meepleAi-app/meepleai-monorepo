using Api.BoundedContexts.GameManagement.Domain.Entities;
using Api.BoundedContexts.GameManagement.Domain.ValueObjects;
using Xunit;
using FluentAssertions;
using Api.Tests.Constants;

namespace Api.Tests.BoundedContexts.GameManagement.Domain;

[Trait("Category", TestCategories.Unit)]

public class GameDomainTests
{
    [Fact]
    public void Game_WithRequiredTitle_CreatesSuccessfully()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var title = new GameTitle("Catan");

        // Act
        var game = new Game(gameId, title);

        // Assert
        game.Id.Should().Be(gameId);
        game.Title.Value.Should().Be("Catan");
        game.Publisher.Should().BeNull();
        game.YearPublished.Should().BeNull();
        game.PlayerCount.Should().BeNull();
        game.PlayTime.Should().BeNull();
    }

    [Fact]
    public void Game_WithAllDetails_CreatesSuccessfully()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var title = new GameTitle("Ticket to Ride");
        var publisher = new Publisher("Days of Wonder");
        var year = new YearPublished(2004);
        var playerCount = new PlayerCount(2, 5);
        var playTime = new PlayTime(45, 60);

        // Act
        var game = new Game(gameId, title, publisher, year, playerCount, playTime);

        // Assert
        game.Title.Value.Should().Be("Ticket to Ride");
        game.Publisher?.Name.Should().Be("Days of Wonder");
        game.YearPublished?.Value.Should().Be(2004);
        game.PlayerCount?.Min.Should().Be(2);
        game.PlayTime?.MinMinutes.Should().Be(45);
    }

    [Fact]
    public void Game_UpdateDetails_ModifiesProperties()
    {
        // Arrange
        var game = new Game(Guid.NewGuid(), new GameTitle("Catan"));
        var newPublisher = new Publisher("KOSMOS");
        var newYear = new YearPublished(1995);

        // Act
        game.UpdateDetails(publisher: newPublisher, yearPublished: newYear);

        // Assert
        game.Publisher?.Name.Should().Be("KOSMOS");
        game.YearPublished?.Value.Should().Be(1995);
        game.Title.Value.Should().Be("Catan"); // Unchanged
    }

    [Fact]
    public void Game_UpdateDetails_WithNewTitle_ChangesTitle()
    {
        // Arrange
        var game = new Game(Guid.NewGuid(), new GameTitle("Catan"));
        var newTitle = new GameTitle("Settlers of Catan");

        // Act
        game.UpdateDetails(title: newTitle);

        // Assert
        game.Title.Value.Should().Be("Settlers of Catan");
    }

    [Fact]
    public void Game_LinkToBgg_StoresBggData()
    {
        // Arrange
        var game = new Game(Guid.NewGuid(), new GameTitle("Catan"));
        var bggId = 13;
        var metadata = "{\"rating\": 7.2}";

        // Act
        game.LinkToBgg(bggId, metadata);

        // Assert
        game.BggId.Should().Be(13);
        game.BggMetadata.Should().Be("{\"rating\": 7.2}");
    }

    [Fact]
    public void Game_LinkToBgg_WithInvalidId_ThrowsArgumentException()
    {
        // Arrange
        var game = new Game(Guid.NewGuid(), new GameTitle("Catan"));

        // Act & Assert
        var exception = ((Action)(() => game.LinkToBgg(0))).Should().Throw<ArgumentException>().Which;
        exception.Message.Should().ContainEquivalentOf("BGG ID must be positive");
    }

    [Fact]
    public void Game_SupportsPlayerCount_ReturnsCorrectly()
    {
        // Arrange
        var playerCount = new PlayerCount(2, 4);
        var game = new Game(Guid.NewGuid(), new GameTitle("Catan"), playerCount: playerCount);

        // Act & Assert
        (game.SupportsPlayerCount(2)).Should().BeTrue();
        (game.SupportsPlayerCount(3)).Should().BeTrue();
        (game.SupportsPlayerCount(4)).Should().BeTrue();
        (game.SupportsPlayerCount(1)).Should().BeFalse();
        (game.SupportsPlayerCount(5)).Should().BeFalse();
    }

    [Fact]
    public void Game_SupportsPlayerCount_WhenNotSpecified_ReturnsTrue()
    {
        // Arrange
        var game = new Game(Guid.NewGuid(), new GameTitle("Generic Game"));

        // Act & Assert
        (game.SupportsPlayerCount(1)).Should().BeTrue();
        (game.SupportsPlayerCount(100)).Should().BeTrue();
    }

    [Fact]
    public void Game_SupportsSolo_WhenMinPlayersIsOne()
    {
        // Arrange
        var soloPlayerCount = new PlayerCount(1, 4);
        var multiplayerCount = new PlayerCount(2, 4);
        var soloGame = new Game(Guid.NewGuid(), new GameTitle("Solo Game"), playerCount: soloPlayerCount);
        var multiGame = new Game(Guid.NewGuid(), new GameTitle("Multi Game"), playerCount: multiplayerCount);

        // Act & Assert
        (soloGame.SupportsSolo).Should().BeTrue();
        (multiGame.SupportsSolo).Should().BeFalse();
    }

    [Fact]
    public void Game_SupportsSolo_WhenPlayerCountNotSpecified_ReturnsFalse()
    {
        // Arrange
        var game = new Game(Guid.NewGuid(), new GameTitle("Generic Game"));

        // Act & Assert
        (game.SupportsSolo).Should().BeFalse();
    }

    [Fact]
    public void Game_CreatedAt_IsSetToCurrentTime()
    {
        // Arrange
        var before = DateTime.UtcNow;

        // Act
        var game = new Game(Guid.NewGuid(), new GameTitle("Catan"));

        // Assert
        var after = DateTime.UtcNow;
        game.CreatedAt.Should().BeOnOrAfter(before).And.BeOnOrBefore(after);
    }
}

