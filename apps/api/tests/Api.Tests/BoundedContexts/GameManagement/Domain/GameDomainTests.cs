using Api.BoundedContexts.GameManagement.Domain.Entities;
using Api.BoundedContexts.GameManagement.Domain.ValueObjects;
using Xunit;

namespace Api.Tests.BoundedContexts.GameManagement.Domain;

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
        Assert.Equal(gameId, game.Id);
        Assert.Equal("Catan", game.Title.Value);
        Assert.Null(game.Publisher);
        Assert.Null(game.YearPublished);
        Assert.Null(game.PlayerCount);
        Assert.Null(game.PlayTime);
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
        Assert.Equal("Ticket to Ride", game.Title.Value);
        Assert.Equal("Days of Wonder", game.Publisher?.Name);
        Assert.Equal(2004, game.YearPublished?.Value);
        Assert.Equal(2, game.PlayerCount?.Min);
        Assert.Equal(45, game.PlayTime?.MinMinutes);
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
        Assert.Equal("KOSMOS", game.Publisher?.Name);
        Assert.Equal(1995, game.YearPublished?.Value);
        Assert.Equal("Catan", game.Title.Value); // Unchanged
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
        Assert.Equal("Settlers of Catan", game.Title.Value);
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
        Assert.Equal(13, game.BggId);
        Assert.Equal("{\"rating\": 7.2}", game.BggMetadata);
    }

    [Fact]
    public void Game_LinkToBgg_WithInvalidId_ThrowsArgumentException()
    {
        // Arrange
        var game = new Game(Guid.NewGuid(), new GameTitle("Catan"));

        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() => game.LinkToBgg(0));
        Assert.Contains("BGG ID must be positive", exception.Message);
    }

    [Fact]
    public void Game_SupportsPlayerCount_ReturnsCorrectly()
    {
        // Arrange
        var playerCount = new PlayerCount(2, 4);
        var game = new Game(Guid.NewGuid(), new GameTitle("Catan"), playerCount: playerCount);

        // Act & Assert
        Assert.True(game.SupportsPlayerCount(2));
        Assert.True(game.SupportsPlayerCount(3));
        Assert.True(game.SupportsPlayerCount(4));
        Assert.False(game.SupportsPlayerCount(1));
        Assert.False(game.SupportsPlayerCount(5));
    }

    [Fact]
    public void Game_SupportsPlayerCount_WhenNotSpecified_ReturnsTrue()
    {
        // Arrange
        var game = new Game(Guid.NewGuid(), new GameTitle("Generic Game"));

        // Act & Assert
        Assert.True(game.SupportsPlayerCount(1));
        Assert.True(game.SupportsPlayerCount(100));
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
        Assert.True(soloGame.SupportsSolo);
        Assert.False(multiGame.SupportsSolo);
    }

    [Fact]
    public void Game_SupportsSolo_WhenPlayerCountNotSpecified_ReturnsFalse()
    {
        // Arrange
        var game = new Game(Guid.NewGuid(), new GameTitle("Generic Game"));

        // Act & Assert
        Assert.False(game.SupportsSolo);
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
        Assert.InRange(game.CreatedAt, before, after);
    }
}

