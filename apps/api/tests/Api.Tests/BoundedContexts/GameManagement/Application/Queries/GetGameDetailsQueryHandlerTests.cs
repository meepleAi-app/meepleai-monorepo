using Api.BoundedContexts.GameManagement.Application.Handlers;
using Api.BoundedContexts.GameManagement.Application.Queries;
using Api.BoundedContexts.GameManagement.Domain.Entities;
using Api.BoundedContexts.GameManagement.Domain.Repositories;
using Api.BoundedContexts.GameManagement.Domain.ValueObjects;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.GameManagement.Application.Queries;

/// <summary>
/// Comprehensive tests for GetGameDetailsQueryHandler.
/// Tests game detail retrieval with extended metadata and statistics.
/// </summary>
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
            _sessionRepositoryMock.Object
        );
    }

    #region Happy Path Tests

    [Fact]
    public async Task Handle_WithExistingGame_ReturnsGameDetailsDto()
    {
        // Arrange
        var game = CreateTestGame();
        var sessions = new List<GameSession>();
        var query = new GetGameDetailsQuery(game.Id);

        _gameRepositoryMock
            .Setup(x => x.GetByIdAsync(game.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(game);

        _sessionRepositoryMock
            .Setup(x => x.FindByGameIdAsync(game.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(sessions);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(game.Id, result.Id);
        Assert.Equal("Test Game", result.Title);
        Assert.Equal("Test Publisher", result.Publisher);
        Assert.Equal(2023, result.YearPublished);
        Assert.Equal(2, result.MinPlayers);
        Assert.Equal(4, result.MaxPlayers);
        Assert.Equal(30, result.MinPlayTimeMinutes);
        Assert.Equal(60, result.MaxPlayTimeMinutes);
        Assert.False(result.SupportsSolo);
        Assert.Null(result.TotalSessionsPlayed);
        Assert.Null(result.LastPlayedAt);

        _gameRepositoryMock.Verify(x => x.GetByIdAsync(game.Id, It.IsAny<CancellationToken>()), Times.Once);
        _sessionRepositoryMock.Verify(x => x.FindByGameIdAsync(game.Id, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WithPlayStatistics_ReturnsStatistics()
    {
        // Arrange
        var game = CreateTestGame();
        var completedSession1 = CreateCompletedSession(game.Id, DateTime.UtcNow.AddDays(-7));
        var completedSession2 = CreateCompletedSession(game.Id, DateTime.UtcNow.AddDays(-3));
        var activeSession = CreateActiveSession(game.Id);

        var sessions = new List<GameSession> { completedSession1, completedSession2, activeSession };
        var query = new GetGameDetailsQuery(game.Id);

        _gameRepositoryMock
            .Setup(x => x.GetByIdAsync(game.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(game);

        _sessionRepositoryMock
            .Setup(x => x.FindByGameIdAsync(game.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(sessions);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.TotalSessionsPlayed); // Only completed sessions
        Assert.NotNull(result.LastPlayedAt);
        Assert.True(result.LastPlayedAt <= DateTime.UtcNow);
    }

    [Fact]
    public async Task Handle_WithSoloSupport_ReturnsSoloFlag()
    {
        // Arrange
        var game = new Game(
            id: Guid.NewGuid(),
            title: new GameTitle("Solo Game"),
            publisher: new Publisher("Publisher"),
            yearPublished: new YearPublished(2023),
            playerCount: new PlayerCount(1, 2), // Supports solo (min 1)
            playTime: new PlayTime(30, 60)
        );

        var sessions = new List<GameSession>();
        var query = new GetGameDetailsQuery(game.Id);

        _gameRepositoryMock
            .Setup(x => x.GetByIdAsync(game.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(game);

        _sessionRepositoryMock
            .Setup(x => x.FindByGameIdAsync(game.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(sessions);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.SupportsSolo);
    }

    [Fact]
    public async Task Handle_WithBggData_ReturnsBggFields()
    {
        // Arrange
        var game = CreateTestGame();
        game.LinkToBgg(123456, "{\"rating\": 8.5}");

        var sessions = new List<GameSession>();
        var query = new GetGameDetailsQuery(game.Id);

        _gameRepositoryMock
            .Setup(x => x.GetByIdAsync(game.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(game);

        _sessionRepositoryMock
            .Setup(x => x.FindByGameIdAsync(game.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(sessions);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(123456, result.BggId);
        Assert.Equal("{\"rating\": 8.5}", result.BggMetadata);
    }

    #endregion

    #region Game Not Found Tests

    [Fact]
    public async Task Handle_WithNonExistentGame_ReturnsNull()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var query = new GetGameDetailsQuery(gameId);

        _gameRepositoryMock
            .Setup(x => x.GetByIdAsync(gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Game?)null);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.Null(result);

        _gameRepositoryMock.Verify(x => x.GetByIdAsync(gameId, It.IsAny<CancellationToken>()), Times.Once);
        _sessionRepositoryMock.Verify(x => x.FindByGameIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    #endregion

    #region Edge Cases

    [Fact]
    public async Task Handle_WithCancellationToken_PassesToRepository()
    {
        // Arrange
        var game = CreateTestGame();
        var sessions = new List<GameSession>();
        var query = new GetGameDetailsQuery(game.Id);

        var cts = new CancellationTokenSource();
        var token = cts.Token;

        _gameRepositoryMock
            .Setup(x => x.GetByIdAsync(game.Id, token))
            .ReturnsAsync(game);

        _sessionRepositoryMock
            .Setup(x => x.FindByGameIdAsync(game.Id, token))
            .ReturnsAsync(sessions);

        // Act
        await _handler.Handle(query, token);

        // Assert
        _gameRepositoryMock.Verify(x => x.GetByIdAsync(game.Id, token), Times.Once);
        _sessionRepositoryMock.Verify(x => x.FindByGameIdAsync(game.Id, token), Times.Once);
    }

    [Fact]
    public async Task Handle_WithOnlyActiveSessions_ExcludesFromStatistics()
    {
        // Arrange
        var game = CreateTestGame();
        var activeSession = CreateActiveSession(game.Id);
        var sessions = new List<GameSession> { activeSession };
        var query = new GetGameDetailsQuery(game.Id);

        _gameRepositoryMock
            .Setup(x => x.GetByIdAsync(game.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(game);

        _sessionRepositoryMock
            .Setup(x => x.FindByGameIdAsync(game.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(sessions);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Null(result.TotalSessionsPlayed); // No completed sessions
        Assert.Null(result.LastPlayedAt);
    }

    #endregion

    #region Helper Methods

    private Game CreateTestGame()
    {
        return new Game(
            id: Guid.NewGuid(),
            title: new GameTitle("Test Game"),
            publisher: new Publisher("Test Publisher"),
            yearPublished: new YearPublished(2023),
            playerCount: new PlayerCount(2, 4),
            playTime: new PlayTime(30, 60)
        );
    }

    private GameSession CreateCompletedSession(Guid gameId, DateTime completedAt)
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

        // Complete the session using domain method
        session.Start();
        session.Complete("Player 1");

        // Use reflection to set CompletedAt to specific time for testing
        session.GetType().GetProperty("CompletedAt")?.SetValue(session, completedAt);

        return session;
    }

    private GameSession CreateActiveSession(Guid gameId)
    {
        var players = new List<SessionPlayer>
        {
            new SessionPlayer("Player 1", 1, "Red")
        };

        return new GameSession(
            id: Guid.NewGuid(),
            gameId: gameId,
            players: players
        );
    }

    #endregion
}
