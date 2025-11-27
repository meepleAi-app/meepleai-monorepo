using Api.BoundedContexts.GameManagement.Application.Handlers;
using Api.BoundedContexts.GameManagement.Application.Queries;
using Api.BoundedContexts.GameManagement.Domain.Entities;
using Api.BoundedContexts.GameManagement.Domain.Repositories;
using Api.Tests.BoundedContexts.GameManagement.TestHelpers;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.GameManagement.Application.Handlers;

/// <summary>
/// Comprehensive tests for GetGameByIdQueryHandler.
/// Tests single game retrieval and DTO mapping.
/// </summary>
public class GetGameByIdQueryHandlerTests
{
    private readonly Mock<IGameRepository> _gameRepositoryMock;
    private readonly GetGameByIdQueryHandler _handler;

    public GetGameByIdQueryHandlerTests()
    {
        _gameRepositoryMock = new Mock<IGameRepository>();
        _handler = new GetGameByIdQueryHandler(_gameRepositoryMock.Object);
    }

    #region Happy Path Tests

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
        Assert.NotNull(result);
        Assert.Equal(gameId, result.Id);
        Assert.Equal("Catan", result.Title);
        Assert.Equal("Kosmos", result.Publisher);
        Assert.Equal(1995, result.YearPublished);
        Assert.Equal(3, result.MinPlayers);
        Assert.Equal(4, result.MaxPlayers);
        Assert.Equal(60, result.MinPlayTimeMinutes);
        Assert.Equal(120, result.MaxPlayTimeMinutes);

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
        Assert.NotNull(result);
        Assert.Equal(gameId, result.Id);
        Assert.Equal("Chess", result.Title);
        Assert.Null(result.Publisher);
        Assert.Null(result.YearPublished);
        Assert.Null(result.MinPlayers);
        Assert.Null(result.MaxPlayers);
        Assert.Null(result.MinPlayTimeMinutes);
        Assert.Null(result.MaxPlayTimeMinutes);
        Assert.Null(result.BggId);
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
        Assert.NotNull(result);
        Assert.Equal(30549, result.BggId);
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
        Assert.NotNull(result);
        Assert.NotNull(result.Publisher);
        Assert.NotNull(result.YearPublished);
        Assert.NotNull(result.MinPlayers);
        Assert.NotNull(result.MaxPlayers);
        Assert.NotNull(result.MinPlayTimeMinutes);
        Assert.NotNull(result.MaxPlayTimeMinutes);
        Assert.NotNull(result.BggId);
    }

    #endregion

    #region Edge Cases

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
        Assert.Null(result);

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
        Assert.Null(result);
    }

    #endregion

    #region Cancellation Tests

    [Fact]
    public async Task Handle_WithCancellationToken_PassesToRepository()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var game = new GameBuilder()
            .WithId(gameId)
            .WithTitle("Test Game")
            .Build();

        var cts = new CancellationTokenSource();
        var cancellationToken = cts.Token;

        _gameRepositoryMock
            .Setup(r => r.GetByIdAsync(gameId, cancellationToken))
            .ReturnsAsync(game);

        var query = new GetGameByIdQuery(gameId);

        // Act
        var result = await _handler.Handle(query, cancellationToken);

        // Assert
        Assert.NotNull(result);
        _gameRepositoryMock.Verify(
            r => r.GetByIdAsync(gameId, cancellationToken),
            Times.Once);
    }

    #endregion

    #region DTO Mapping Tests

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
        Assert.NotNull(result);
        Assert.NotEqual(default(DateTime), result.CreatedAt);
        Assert.Equal(game.CreatedAt, result.CreatedAt);
    }

    #endregion
}

