using Api.BoundedContexts.GameManagement.Application.Commands;
using Api.BoundedContexts.GameManagement.Application.Queries;
using Api.BoundedContexts.GameManagement.Domain.Entities;
using Api.BoundedContexts.GameManagement.Domain.Repositories;
using Api.Tests.BoundedContexts.GameManagement.TestHelpers;
using Moq;
using Xunit;
using Api.Tests.Constants;

namespace Api.Tests.BoundedContexts.GameManagement.Application.Handlers;

/// <summary>
/// Comprehensive tests for GetAllGamesQueryHandler.
/// Tests game catalog retrieval and DTO mapping.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class GetAllGamesQueryHandlerTests
{
    private readonly Mock<IGameRepository> _gameRepositoryMock;
    private readonly GetAllGamesQueryHandler _handler;

    public GetAllGamesQueryHandlerTests()
    {
        _gameRepositoryMock = new Mock<IGameRepository>();
        _handler = new GetAllGamesQueryHandler(_gameRepositoryMock.Object);
    }
    [Fact]
    public async Task Handle_WithMultipleGames_ReturnsAllMappedDtos()
    {
        // Arrange
        var games = new List<Game>
        {
            new GameBuilder()
                .WithTitle("Catan")
                .WithPublisher("Kosmos")
                .WithYearPublished(1995)
                .WithPlayerCount(3, 4)
                .Build(),
            new GameBuilder()
                .WithTitle("Pandemic")
                .WithPublisher("Z-Man Games")
                .WithYearPublished(2008)
                .WithPlayerCount(2, 4)
                .WithPlayTime(45, 60)
                .Build(),
            new GameBuilder()
                .WithTitle("Ticket to Ride")
                .WithAllDetails()
                .Build()
        };

        _gameRepositoryMock
            .Setup(r => r.GetPaginatedAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((games, games.Count));

        var query = new GetAllGamesQuery();

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(3, result.Total);
        Assert.Equal(3, result.Games.Count);

        Assert.Equal("Catan", result.Games[0].Title);
        Assert.Equal("Kosmos", result.Games[0].Publisher);
        Assert.Equal(1995, result.Games[0].YearPublished);

        Assert.Equal("Pandemic", result.Games[1].Title);
        Assert.Equal(2, result.Games[1].MinPlayers);
        Assert.Equal(4, result.Games[1].MaxPlayers);

        Assert.Equal("Ticket to Ride", result.Games[2].Title);

        _gameRepositoryMock.Verify(
            r => r.GetPaginatedAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WithSingleGame_ReturnsSingleElementList()
    {
        // Arrange
        var games = new List<Game>
        {
            new GameBuilder()
                .WithTitle("Chess")
                .Build()
        };

        _gameRepositoryMock
            .Setup(r => r.GetPaginatedAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((games, games.Count));

        var query = new GetAllGamesQuery();

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Single(result.Games);
        Assert.Equal("Chess", result.Games[0].Title);
    }

    [Fact]
    public async Task Handle_WithNoGames_ReturnsEmptyList()
    {
        // Arrange
        var games = new List<Game>();

        _gameRepositoryMock
            .Setup(r => r.GetPaginatedAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((games, 0));

        var query = new GetAllGamesQuery();

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result.Games);
        Assert.Equal(0, result.Total);
    }

    [Fact]
    public async Task Handle_WithVariedGameProperties_MapsAllCorrectly()
    {
        // Arrange
        var games = new List<Game>
        {
            // Minimal game
            new GameBuilder()
                .WithTitle("Simple Game")
                .Build(),

            // Game with publisher only
            new GameBuilder()
                .WithTitle("Published Game")
                .WithPublisher("Publisher")
                .Build(),

            // Game with full details
            new GameBuilder()
                .WithTitle("Complete Game")
                .WithAllDetails()
                .Build()
        };

        _gameRepositoryMock
            .Setup(r => r.GetPaginatedAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((games, games.Count));

        var query = new GetAllGamesQuery();

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(3, result.Games.Count);

        // Minimal game
        Assert.Equal("Simple Game", result.Games[0].Title);
        Assert.Null(result.Games[0].Publisher);
        Assert.Null(result.Games[0].YearPublished);

        // Game with publisher
        Assert.Equal("Published Game", result.Games[1].Title);
        Assert.Equal("Publisher", result.Games[1].Publisher);

        // Complete game
        Assert.Equal("Complete Game", result.Games[2].Title);
        Assert.NotNull(result.Games[2].Publisher);
        Assert.NotNull(result.Games[2].YearPublished);
        Assert.NotNull(result.Games[2].MinPlayers);
    }

    [Fact]
    public async Task Handle_WithBggLinkedGames_IncludesBggIds()
    {
        // Arrange
        var games = new List<Game>
        {
            new GameBuilder()
                .WithTitle("Catan")
                .WithBggLink(13, "{\"rating\":7.2}")
                .Build(),
            new GameBuilder()
                .WithTitle("Pandemic")
                .WithBggLink(30549)
                .Build(),
            new GameBuilder()
                .WithTitle("Chess")
                .Build() // No BGG link
        };

        _gameRepositoryMock
            .Setup(r => r.GetPaginatedAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((games, games.Count));

        var query = new GetAllGamesQuery();

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(3, result.Games.Count);
        Assert.Equal(13, result.Games[0].BggId);
        Assert.Equal(30549, result.Games[1].BggId);
        Assert.Null(result.Games[2].BggId);
    }
    [Fact]
    public async Task Handle_WithCancellationToken_PassesToRepository()
    {
        // Arrange
        var games = new List<Game>
        {
            new GameBuilder().WithTitle("Game").Build()
        };

        using var cts = new CancellationTokenSource();
        var cancellationToken = cts.Token;

        _gameRepositoryMock
            .Setup(r => r.GetPaginatedAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(), cancellationToken))
            .ReturnsAsync((games, games.Count));

        var query = new GetAllGamesQuery();

        // Act
        var result = await _handler.Handle(query, cancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Single(result.Games);

        _gameRepositoryMock.Verify(
            r => r.GetPaginatedAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(), cancellationToken),
            Times.Once);
    }
    [Fact]
    public async Task Handle_MapsAllPropertiesToDto()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var createdAt = DateTime.UtcNow.AddDays(-10);

        var games = new List<Game>
        {
            new GameBuilder()
                .WithId(gameId)
                .WithTitle("Wingspan")
                .WithPublisher("Stonemaier Games")
                .WithYearPublished(2019)
                .WithPlayerCount(1, 5)
                .WithPlayTime(40, 70)
                .WithBggLink(266192)
                .Build()
        };

        _gameRepositoryMock
            .Setup(r => r.GetPaginatedAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((games, games.Count));

        var query = new GetAllGamesQuery();

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        var dto = result.Games[0];
        Assert.Equal(gameId, dto.Id);
        Assert.Equal("Wingspan", dto.Title);
        Assert.Equal("Stonemaier Games", dto.Publisher);
        Assert.Equal(2019, dto.YearPublished);
        Assert.Equal(1, dto.MinPlayers);
        Assert.Equal(5, dto.MaxPlayers);
        Assert.Equal(40, dto.MinPlayTimeMinutes);
        Assert.Equal(70, dto.MaxPlayTimeMinutes);
        Assert.Equal(266192, dto.BggId);
        Assert.NotEqual(default(DateTime), dto.CreatedAt);
    }
}

