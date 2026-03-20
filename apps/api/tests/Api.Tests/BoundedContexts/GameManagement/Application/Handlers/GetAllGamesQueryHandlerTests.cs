using Api.BoundedContexts.GameManagement.Application.Commands;
using Api.BoundedContexts.GameManagement.Application.Queries;
using Api.BoundedContexts.GameManagement.Application.Queries;
using Api.BoundedContexts.GameManagement.Domain.Entities;
using Api.BoundedContexts.GameManagement.Domain.Repositories;
using Api.Tests.BoundedContexts.GameManagement.TestHelpers;
using Moq;
using Xunit;
using FluentAssertions;
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
        result.Should().NotBeNull();
        result.Total.Should().Be(3);
        result.Games.Count.Should().Be(3);

        result.Games[0].Title.Should().Be("Catan");
        result.Games[0].Publisher.Should().Be("Kosmos");
        result.Games[0].YearPublished.Should().Be(1995);

        result.Games[1].Title.Should().Be("Pandemic");
        result.Games[1].MinPlayers.Should().Be(2);
        result.Games[1].MaxPlayers.Should().Be(4);

        result.Games[2].Title.Should().Be("Ticket to Ride");

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
        result.Should().NotBeNull();
        result.Games.Should().ContainSingle();
        result.Games[0].Title.Should().Be("Chess");
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
        result.Should().NotBeNull();
        result.Games.Should().BeEmpty();
        result.Total.Should().Be(0);
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
        result.Games.Count.Should().Be(3);

        // Minimal game
        result.Games[0].Title.Should().Be("Simple Game");
        result.Games[0].Publisher.Should().BeNull();
        result.Games[0].YearPublished.Should().BeNull();

        // Game with publisher
        result.Games[1].Title.Should().Be("Published Game");
        result.Games[1].Publisher.Should().Be("Publisher");

        // Complete game
        result.Games[2].Title.Should().Be("Complete Game");
        result.Games[2].Publisher.Should().NotBeNull();
        result.Games[2].YearPublished.Should().NotBeNull();
        result.Games[2].MinPlayers.Should().NotBeNull();
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
        result.Games.Count.Should().Be(3);
        result.Games[0].BggId.Should().Be(13);
        result.Games[1].BggId.Should().Be(30549);
        result.Games[2].BggId.Should().BeNull();
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
        result.Should().NotBeNull();
        result.Games.Should().ContainSingle();

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
        dto.Id.Should().Be(gameId);
        dto.Title.Should().Be("Wingspan");
        dto.Publisher.Should().Be("Stonemaier Games");
        dto.YearPublished.Should().Be(2019);
        dto.MinPlayers.Should().Be(1);
        dto.MaxPlayers.Should().Be(5);
        dto.MinPlayTimeMinutes.Should().Be(40);
        dto.MaxPlayTimeMinutes.Should().Be(70);
        dto.BggId.Should().Be(266192);
        dto.CreatedAt.Should().NotBe(default(DateTime));
    }
}

