using Api.BoundedContexts.GameManagement.Application.Handlers;
using Api.BoundedContexts.GameManagement.Application.Queries;
using Api.BoundedContexts.GameManagement.Domain.Entities;
using Api.BoundedContexts.GameManagement.Domain.Repositories;
using Api.BoundedContexts.GameManagement.Domain.ValueObjects;
using Moq;
using Xunit;
using Api.Tests.Constants;

namespace Api.Tests.BoundedContexts.GameManagement.Application.Handlers;

/// <summary>
/// Tests for GetGameDetailsQueryHandler.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class GetGameDetailsQueryHandlerTests
{
    private readonly Mock<IGameRepository> _gameRepositoryMock;
    private readonly Mock<IGameSessionRepository> _sessionRepositoryMock;
    private readonly GetGameDetailsQueryHandler _handler;

    public GetGameDetailsQueryHandlerTests()
    {
        _gameRepositoryMock = new Mock<IGameRepository>();
        _sessionRepositoryMock = new Mock<IGameSessionRepository>();
        _handler = new GetGameDetailsQueryHandler(
            _gameRepositoryMock.Object,
            _sessionRepositoryMock.Object);
    }

    [Fact]
    public async Task Handle_WithExistingGame_ReturnsGameDetails()
    {
        // Arrange
        var game = CreateGame();
        var query = new GetGameDetailsQuery(game.Id);

        _gameRepositoryMock
            .Setup(r => r.GetByIdAsync(game.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(game);

        _sessionRepositoryMock
            .Setup(r => r.FindByGameIdAsync(game.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<GameSession>());

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(game.Id, result.Id);
        Assert.Equal("Test Game", result.Title);
    }

    [Fact]
    public async Task Handle_WithNonExistentGame_ReturnsNull()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var query = new GetGameDetailsQuery(gameId);

        _gameRepositoryMock
            .Setup(r => r.GetByIdAsync(gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Game?)null);

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task Handle_WithCompletedSessions_ReturnsPlayStatistics()
    {
        // Arrange
        var game = CreateGame();
        var completedSession = CreateCompletedSession(game.Id);
        var query = new GetGameDetailsQuery(game.Id);

        _gameRepositoryMock
            .Setup(r => r.GetByIdAsync(game.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(game);

        _sessionRepositoryMock
            .Setup(r => r.FindByGameIdAsync(game.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<GameSession> { completedSession });

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(1, result.TotalSessionsPlayed);
        Assert.NotNull(result.LastPlayedAt);
    }

    [Fact]
    public async Task Handle_WithNoCompletedSessions_ReturnsNullStatistics()
    {
        // Arrange
        var game = CreateGame();
        var query = new GetGameDetailsQuery(game.Id);

        _gameRepositoryMock
            .Setup(r => r.GetByIdAsync(game.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(game);

        _sessionRepositoryMock
            .Setup(r => r.FindByGameIdAsync(game.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<GameSession>());

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Null(result.TotalSessionsPlayed);
        Assert.Null(result.LastPlayedAt);
    }

    [Fact]
    public async Task Handle_WithCancellationToken_PassesToRepositories()
    {
        // Arrange
        var game = CreateGame();
        var query = new GetGameDetailsQuery(game.Id);

        using var cts = new CancellationTokenSource();
        var token = cts.Token;

        _gameRepositoryMock
            .Setup(r => r.GetByIdAsync(game.Id, token))
            .ReturnsAsync(game);

        _sessionRepositoryMock
            .Setup(r => r.FindByGameIdAsync(game.Id, token))
            .ReturnsAsync(new List<GameSession>());

        // Act
        await _handler.Handle(query, token);

        // Assert
        _gameRepositoryMock.Verify(r => r.GetByIdAsync(game.Id, token), Times.Once);
        _sessionRepositoryMock.Verify(r => r.FindByGameIdAsync(game.Id, token), Times.Once);
    }

    [Fact]
    public async Task Handle_WithNullQuery_ThrowsArgumentNullException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => _handler.Handle(null!, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Handle_MapsAllGameFieldsCorrectly()
    {
        // Arrange
        var game = CreateGameWithAllFields();
        var query = new GetGameDetailsQuery(game.Id);

        _gameRepositoryMock
            .Setup(r => r.GetByIdAsync(game.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(game);

        _sessionRepositoryMock
            .Setup(r => r.FindByGameIdAsync(game.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<GameSession>());

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(game.Id, result.Id);
        Assert.Equal("Complex Game", result.Title);
        Assert.Equal("Test Publisher", result.Publisher);
        Assert.Equal(2020, result.YearPublished);
        Assert.Equal(1, result.MinPlayers);
        Assert.Equal(6, result.MaxPlayers);
        Assert.Equal(30, result.MinPlayTimeMinutes);
        Assert.Equal(120, result.MaxPlayTimeMinutes);
        Assert.True(result.SupportsSolo); // MinPlayers = 1 enables SupportsSolo
    }

    private static Game CreateGame()
    {
        return new Game(
            id: Guid.NewGuid(),
            title: new GameTitle("Test Game"));
    }

    private static Game CreateGameWithAllFields()
    {
        // MinPlayers = 1 enables SupportsSolo (derived from PlayerCount)
        return new Game(
            id: Guid.NewGuid(),
            title: new GameTitle("Complex Game"),
            publisher: new Publisher("Test Publisher"),
            yearPublished: new YearPublished(2020),
            playerCount: new PlayerCount(1, 6),
            playTime: new PlayTime(30, 120));
    }

    private static GameSession CreateCompletedSession(Guid gameId)
    {
        var players = new List<SessionPlayer>
        {
            new SessionPlayer("Player 1", 1, "Red")
        };

        var session = new GameSession(
            id: Guid.NewGuid(),
            gameId: gameId,
            players: players
        );

        session.Start();
        session.Complete("Player 1");

        return session;
    }
}
