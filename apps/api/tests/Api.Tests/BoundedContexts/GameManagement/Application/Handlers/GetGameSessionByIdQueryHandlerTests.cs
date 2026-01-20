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
/// Tests for GetGameSessionByIdQueryHandler.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class GetGameSessionByIdQueryHandlerTests
{
    private readonly Mock<IGameSessionRepository> _sessionRepositoryMock;
    private readonly GetGameSessionByIdQueryHandler _handler;

    public GetGameSessionByIdQueryHandlerTests()
    {
        _sessionRepositoryMock = new Mock<IGameSessionRepository>();
        _handler = new GetGameSessionByIdQueryHandler(_sessionRepositoryMock.Object);
    }

    [Fact]
    public async Task Handle_WithExistingSession_ReturnsSessionDto()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var session = CreateSession(gameId);
        var query = new GetGameSessionByIdQuery(session.Id);

        _sessionRepositoryMock
            .Setup(r => r.GetByIdAsync(session.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(session.Id, result.Id);
        Assert.Equal(gameId, result.GameId);
        Assert.Equal("Setup", result.Status);
        Assert.NotNull(result.Players);
        Assert.Single(result.Players);
    }

    [Fact]
    public async Task Handle_WithNonExistentSession_ReturnsNull()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var query = new GetGameSessionByIdQuery(sessionId);

        _sessionRepositoryMock
            .Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((GameSession?)null);

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task Handle_MapsPlayersCorrectly()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var players = new List<SessionPlayer>
        {
            new SessionPlayer("Player 1", 1, "Red"),
            new SessionPlayer("Player 2", 2, "Blue"),
            new SessionPlayer("Player 3", 3, "Green")
        };

        var session = new GameSession(
            id: Guid.NewGuid(),
            gameId: gameId,
            players: players
        );

        var query = new GetGameSessionByIdQuery(session.Id);

        _sessionRepositoryMock
            .Setup(r => r.GetByIdAsync(session.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(3, result.Players.Count);
        Assert.Equal("Player 1", result.Players[0].PlayerName);
        Assert.Equal(1, result.Players[0].PlayerOrder);
        Assert.Equal("Red", result.Players[0].Color);
    }

    [Fact]
    public async Task Handle_WithCancellationToken_PassesToRepository()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var query = new GetGameSessionByIdQuery(sessionId);
        using var cts = new CancellationTokenSource();
        var token = cts.Token;

        _sessionRepositoryMock
            .Setup(r => r.GetByIdAsync(sessionId, token))
            .ReturnsAsync((GameSession?)null);

        // Act
        await _handler.Handle(query, token);

        // Assert
        _sessionRepositoryMock.Verify(r => r.GetByIdAsync(sessionId, token), Times.Once);
    }

    [Fact]
    public async Task Handle_WithNullQuery_ThrowsArgumentNullException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => _handler.Handle(null!, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Handle_WithCompletedSession_MapsDurationCorrectly()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var session = CreateSession(gameId);
        session.Start();
        session.Complete("Player 1");
        var query = new GetGameSessionByIdQuery(session.Id);

        _sessionRepositoryMock
            .Setup(r => r.GetByIdAsync(session.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Completed", result.Status);
        Assert.Equal("Player 1", result.WinnerName);
        Assert.NotNull(result.CompletedAt);
    }

    private static GameSession CreateSession(Guid gameId)
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
}
