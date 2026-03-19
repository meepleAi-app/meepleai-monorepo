using Api.BoundedContexts.GameManagement.Application.DTOs;
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
/// Tests for GetActiveSessionsQueryHandler.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class GetActiveSessionsQueryHandlerTests
{
    private readonly Mock<IGameSessionRepository> _sessionRepositoryMock;
    private readonly GetActiveSessionsQueryHandler _handler;

    public GetActiveSessionsQueryHandlerTests()
    {
        _sessionRepositoryMock = new Mock<IGameSessionRepository>();
        _handler = new GetActiveSessionsQueryHandler(_sessionRepositoryMock.Object);
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
        var query = new GetActiveSessionsQuery();

        _sessionRepositoryMock
            .Setup(r => r.FindActiveAsync(null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(sessions);
        _sessionRepositoryMock
            .Setup(r => r.CountActiveAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(2);

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.Sessions.Count);
        Assert.Equal(2, result.Total);
    }

    [Fact]
    public async Task Handle_WithNoActiveSessions_ReturnsEmptyList()
    {
        // Arrange
        var query = new GetActiveSessionsQuery();

        _sessionRepositoryMock
            .Setup(r => r.FindActiveAsync(null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<GameSession>());
        _sessionRepositoryMock
            .Setup(r => r.CountActiveAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result.Sessions);
        Assert.Equal(0, result.Total);
    }

    [Fact]
    public async Task Handle_WithLimit_PassesToRepository()
    {
        // Arrange
        var query = new GetActiveSessionsQuery(Limit: 10);

        _sessionRepositoryMock
            .Setup(r => r.FindActiveAsync(10, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<GameSession>());
        _sessionRepositoryMock
            .Setup(r => r.CountActiveAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        // Act
        await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        _sessionRepositoryMock.Verify(r => r.FindActiveAsync(10, null, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WithOffset_PassesToRepository()
    {
        // Arrange
        var query = new GetActiveSessionsQuery(Offset: 5);

        _sessionRepositoryMock
            .Setup(r => r.FindActiveAsync(null, 5, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<GameSession>());
        _sessionRepositoryMock
            .Setup(r => r.CountActiveAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        // Act
        await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        _sessionRepositoryMock.Verify(r => r.FindActiveAsync(null, 5, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WithLimitAndOffset_PassesBothToRepository()
    {
        // Arrange
        var query = new GetActiveSessionsQuery(Limit: 10, Offset: 5);

        _sessionRepositoryMock
            .Setup(r => r.FindActiveAsync(10, 5, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<GameSession>());
        _sessionRepositoryMock
            .Setup(r => r.CountActiveAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        // Act
        await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        _sessionRepositoryMock.Verify(r => r.FindActiveAsync(10, 5, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WithNegativeLimit_ThrowsArgumentException()
    {
        // Arrange
        var query = new GetActiveSessionsQuery(Limit: -1);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(
            () => _handler.Handle(query, TestContext.Current.CancellationToken));

        Assert.Contains("non-negative", exception.Message);
    }

    [Fact]
    public async Task Handle_WithLimitExceeding1000_ThrowsArgumentException()
    {
        // Arrange
        var query = new GetActiveSessionsQuery(Limit: 1001);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(
            () => _handler.Handle(query, TestContext.Current.CancellationToken));

        Assert.Contains("1000", exception.Message);
    }

    [Fact]
    public async Task Handle_WithNegativeOffset_ThrowsArgumentException()
    {
        // Arrange
        var query = new GetActiveSessionsQuery(Offset: -1);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(
            () => _handler.Handle(query, TestContext.Current.CancellationToken));

        Assert.Contains("non-negative", exception.Message);
    }

    [Fact]
    public async Task Handle_WithCancellationToken_PassesToRepository()
    {
        // Arrange
        var query = new GetActiveSessionsQuery();
        using var cts = new CancellationTokenSource();
        var token = cts.Token;

        _sessionRepositoryMock
            .Setup(r => r.FindActiveAsync(null, null, token))
            .ReturnsAsync(new List<GameSession>());
        _sessionRepositoryMock
            .Setup(r => r.CountActiveAsync(token))
            .ReturnsAsync(0);

        // Act
        await _handler.Handle(query, token);

        // Assert
        _sessionRepositoryMock.Verify(r => r.FindActiveAsync(null, null, token), Times.Once);
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
