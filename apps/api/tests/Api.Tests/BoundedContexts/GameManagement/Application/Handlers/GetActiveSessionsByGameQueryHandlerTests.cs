using Api.BoundedContexts.GameManagement.Application.Commands;
using Api.BoundedContexts.GameManagement.Application.Queries;
using Api.BoundedContexts.GameManagement.Application.Queries;
using Api.BoundedContexts.GameManagement.Domain.Entities;
using Api.BoundedContexts.GameManagement.Domain.Repositories;
using Api.BoundedContexts.GameManagement.Domain.ValueObjects;
using Moq;
using Xunit;
using Api.Tests.Constants;

namespace Api.Tests.BoundedContexts.GameManagement.Application.Handlers;

/// <summary>
/// Tests for GetActiveSessionsByGameQueryHandler.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class GetActiveSessionsByGameQueryHandlerTests
{
    private readonly Mock<IGameSessionRepository> _sessionRepositoryMock;
    private readonly GetActiveSessionsByGameQueryHandler _handler;

    public GetActiveSessionsByGameQueryHandlerTests()
    {
        _sessionRepositoryMock = new Mock<IGameSessionRepository>();
        _handler = new GetActiveSessionsByGameQueryHandler(_sessionRepositoryMock.Object);
    }

    [Fact]
    public async Task Handle_WithActiveSessions_ReturnsSessionList()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var sessions = new List<GameSession>
        {
            CreateActiveSession(gameId),
            CreateActiveSession(gameId)
        };
        var query = new GetActiveSessionsByGameQuery(gameId);

        _sessionRepositoryMock
            .Setup(r => r.FindActiveByGameIdAsync(gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(sessions);

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.Count);
        Assert.All(result, dto => Assert.Equal(gameId, dto.GameId));
    }

    [Fact]
    public async Task Handle_WithNoActiveSessions_ReturnsEmptyList()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var query = new GetActiveSessionsByGameQuery(gameId);

        _sessionRepositoryMock
            .Setup(r => r.FindActiveByGameIdAsync(gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<GameSession>());

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public async Task Handle_MapsPlayersCorrectly()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var players = new List<SessionPlayer>
        {
            new SessionPlayer("Alice", 1, "Red"),
            new SessionPlayer("Bob", 2, "Blue")
        };

        var session = new GameSession(
            id: Guid.NewGuid(),
            gameId: gameId,
            players: players
        );

        var sessions = new List<GameSession> { session };
        var query = new GetActiveSessionsByGameQuery(gameId);

        _sessionRepositoryMock
            .Setup(r => r.FindActiveByGameIdAsync(gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(sessions);

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Single(result);
        Assert.Equal(2, result[0].Players.Count);
        Assert.Equal("Alice", result[0].Players[0].PlayerName);
        Assert.Equal("Bob", result[0].Players[1].PlayerName);
    }

    [Fact]
    public async Task Handle_WithCancellationToken_PassesToRepository()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var query = new GetActiveSessionsByGameQuery(gameId);
        using var cts = new CancellationTokenSource();
        var token = cts.Token;

        _sessionRepositoryMock
            .Setup(r => r.FindActiveByGameIdAsync(gameId, token))
            .ReturnsAsync(new List<GameSession>());

        // Act
        await _handler.Handle(query, token);

        // Assert
        _sessionRepositoryMock.Verify(r => r.FindActiveByGameIdAsync(gameId, token), Times.Once);
    }

    [Fact]
    public async Task Handle_WithDifferentGameIds_OnlyReturnsMatchingGame()
    {
        // Arrange
        var gameId1 = Guid.NewGuid();
        var gameId2 = Guid.NewGuid();
        var session1 = CreateActiveSession(gameId1);
        var query = new GetActiveSessionsByGameQuery(gameId1);

        _sessionRepositoryMock
            .Setup(r => r.FindActiveByGameIdAsync(gameId1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<GameSession> { session1 });

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Single(result);
        Assert.Equal(gameId1, result[0].GameId);
    }

    private static GameSession CreateActiveSession(Guid gameId)
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
