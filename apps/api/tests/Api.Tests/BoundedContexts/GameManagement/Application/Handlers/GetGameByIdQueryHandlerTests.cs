using Api.BoundedContexts.GameManagement.Application.Commands;
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
/// Comprehensive tests for GetGameByIdQueryHandler.
/// Tests single game retrieval and DTO mapping.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class GetGameByIdQueryHandlerTests
{
    private readonly Mock<IGameRepository> _gameRepositoryMock;
    private readonly GetGameByIdQueryHandler _handler;

    public GetGameByIdQueryHandlerTests()
    {
        _gameRepositoryMock = new Mock<IGameRepository>();
        _handler = new GetGameByIdQueryHandler(_gameRepositoryMock.Object);
    }
    [Fact]
    public async Task Handle_ExistingGame_ReturnsMappedDto()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var game = new GameBuilder()
            .WithId(gameId)
            .WithTitle("Catan")
            .WithPublisher("Kosmos")
            .WithYearPublished(1995)
            .WithPlayerCount(3, 4)
            .WithPlayTime(60, 120)
            .Build();

        _gameRepositoryMock
            .Setup(r => r.GetByIdAsync(gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(game);

        var query = new GetGameByIdQuery(gameId);

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be(gameId);
        result.Title.Should().Be("Catan");
        result.Publisher.Should().Be("Kosmos");
        result.YearPublished.Should().Be(1995);
        result.MinPlayers.Should().Be(3);
        result.MaxPlayers.Should().Be(4);
        result.MinPlayTimeMinutes.Should().Be(60);
        result.MaxPlayTimeMinutes.Should().Be(120);

        _gameRepositoryMock.Verify(
            r => r.GetByIdAsync(gameId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_MinimalGame_ReturnsDtoWithNullOptionalFields()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var game = new GameBuilder()
            .WithId(gameId)
            .WithTitle("Chess")
            .Build();

        _gameRepositoryMock
            .Setup(r => r.GetByIdAsync(gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(game);

        var query = new GetGameByIdQuery(gameId);

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be(gameId);
        result.Title.Should().Be("Chess");
        result.Publisher.Should().BeNull();
        result.YearPublished.Should().BeNull();
        result.MinPlayers.Should().BeNull();
        result.MaxPlayers.Should().BeNull();
        result.MinPlayTimeMinutes.Should().BeNull();
        result.MaxPlayTimeMinutes.Should().BeNull();
        result.BggId.Should().BeNull();
    }

    [Fact]
    public async Task Handle_GameWithBggLink_IncludesBggId()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var game = new GameBuilder()
            .WithId(gameId)
            .WithTitle("Pandemic")
            .WithBggLink(30549, "{\"rating\":7.6}")
            .Build();

        _gameRepositoryMock
            .Setup(r => r.GetByIdAsync(gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(game);

        var query = new GetGameByIdQuery(gameId);

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.BggId.Should().Be(30549);
    }

    [Fact]
    public async Task Handle_GameWithAllDetails_MapsAllProperties()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var game = new GameBuilder()
            .WithId(gameId)
            .WithTitle("Wingspan")
            .WithAllDetails() // Sets all properties
            .WithBggLink(266192)
            .Build();

        _gameRepositoryMock
            .Setup(r => r.GetByIdAsync(gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(game);

        var query = new GetGameByIdQuery(gameId);

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.Publisher.Should().NotBeNull();
        result.YearPublished.Should().NotBeNull();
        result.MinPlayers.Should().NotBeNull();
        result.MaxPlayers.Should().NotBeNull();
        result.MinPlayTimeMinutes.Should().NotBeNull();
        result.MaxPlayTimeMinutes.Should().NotBeNull();
        result.BggId.Should().NotBeNull();
    }
    [Fact]
    public async Task Handle_NonExistentGame_ReturnsNull()
    {
        // Arrange
        var gameId = Guid.NewGuid();

        _gameRepositoryMock
            .Setup(r => r.GetByIdAsync(gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Game?)null);

        var query = new GetGameByIdQuery(gameId);

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        result.Should().BeNull();

        _gameRepositoryMock.Verify(
            r => r.GetByIdAsync(gameId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_EmptyGuid_ReturnsNull()
    {
        // Arrange
        var gameId = Guid.Empty;

        _gameRepositoryMock
            .Setup(r => r.GetByIdAsync(gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Game?)null);

        var query = new GetGameByIdQuery(gameId);

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        result.Should().BeNull();
    }
    [Fact]
    public async Task Handle_WithCancellationToken_PassesToRepository()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var game = new GameBuilder()
            .WithId(gameId)
            .WithTitle("Test Game")
            .Build();

        using var cts = new CancellationTokenSource();
        var cancellationToken = cts.Token;

        _gameRepositoryMock
            .Setup(r => r.GetByIdAsync(gameId, cancellationToken))
            .ReturnsAsync(game);

        var query = new GetGameByIdQuery(gameId);

        // Act
        var result = await _handler.Handle(query, cancellationToken);

        // Assert
        result.Should().NotBeNull();
        _gameRepositoryMock.Verify(
            r => r.GetByIdAsync(gameId, cancellationToken),
            Times.Once);
    }
    [Fact]
    public async Task Handle_PreservesCreatedAt()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var createdAt = DateTime.UtcNow.AddDays(-30);
        var game = new GameBuilder()
            .WithId(gameId)
            .WithTitle("Old Game")
            .Build();

        _gameRepositoryMock
            .Setup(r => r.GetByIdAsync(gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(game);

        var query = new GetGameByIdQuery(gameId);

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.CreatedAt.Should().NotBe(default(DateTime));
        result.CreatedAt.Should().Be(game.CreatedAt);
    }
}


