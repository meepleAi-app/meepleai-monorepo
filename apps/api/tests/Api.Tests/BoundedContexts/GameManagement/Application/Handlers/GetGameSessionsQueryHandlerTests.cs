using Api.BoundedContexts.GameManagement.Application.Commands;
using Api.BoundedContexts.GameManagement.Application.Queries;
using Api.BoundedContexts.GameManagement.Domain.Entities;
using Api.BoundedContexts.GameManagement.Domain.Repositories;
using Api.BoundedContexts.GameManagement.Domain.ValueObjects;
using Moq;
using Xunit;
using Api.Tests.Constants;

namespace Api.Tests.BoundedContexts.GameManagement.Application.Handlers;

/// <summary>
/// Tests for GetGameSessionsQueryHandler.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class GetGameSessionsQueryHandlerTests
{
    private readonly Mock<IGameSessionRepository> _sessionRepositoryMock;
    private readonly GetGameSessionsQueryHandler _handler;

    public GetGameSessionsQueryHandlerTests()
    {
        _sessionRepositoryMock = new Mock<IGameSessionRepository>();
        _handler = new GetGameSessionsQueryHandler(_sessionRepositoryMock.Object);
    }

    [Fact]
    public async Task Handle_WithExistingSessions_ReturnsSessionList()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var sessions = new List<GameSession>
        {
            CreateSession(gameId),
            CreateSession(gameId)
        };
        var query = new GetGameSessionsQuery(gameId);

        _sessionRepositoryMock
            .Setup(r => r.FindByGameIdAsync(gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(sessions);

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.Count);
    }

    [Fact]
    public async Task Handle_WithNoSessions_ReturnsEmptyList()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var query = new GetGameSessionsQuery(gameId);

        _sessionRepositoryMock
            .Setup(r => r.FindByGameIdAsync(gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<GameSession>());

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public async Task Handle_WithPagination_ReturnsPagedResults()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var sessions = Enumerable.Range(1, 10).Select(_ => CreateSession(gameId)).ToList();
        var query = new GetGameSessionsQuery(gameId, PageNumber: 2, PageSize: 3);

        _sessionRepositoryMock
            .Setup(r => r.FindByGameIdAsync(gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(sessions);

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(3, result.Count); // Page 2 with size 3 should return 3 items (items 4-6)
    }

    [Fact]
    public async Task Handle_WithoutPagination_ReturnsAllSessions()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var sessions = Enumerable.Range(1, 5).Select(_ => CreateSession(gameId)).ToList();
        var query = new GetGameSessionsQuery(gameId);

        _sessionRepositoryMock
            .Setup(r => r.FindByGameIdAsync(gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(sessions);

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(5, result.Count);
    }

    [Fact]
    public async Task Handle_WithInvalidPageSize_ThrowsArgumentException()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var query = new GetGameSessionsQuery(gameId, PageSize: 0);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(
            () => _handler.Handle(query, TestContext.Current.CancellationToken));

        Assert.Contains("positive", exception.Message);
    }

    [Fact]
    public async Task Handle_WithPageSizeExceeding1000_ThrowsArgumentException()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var query = new GetGameSessionsQuery(gameId, PageSize: 1001);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(
            () => _handler.Handle(query, TestContext.Current.CancellationToken));

        Assert.Contains("1000", exception.Message);
    }

    [Fact]
    public async Task Handle_WithInvalidPageNumber_ThrowsArgumentException()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var query = new GetGameSessionsQuery(gameId, PageNumber: 0);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(
            () => _handler.Handle(query, TestContext.Current.CancellationToken));

        Assert.Contains("1-based", exception.Message);
    }

    [Fact]
    public async Task Handle_WithCancellationToken_PassesToRepository()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var query = new GetGameSessionsQuery(gameId);
        using var cts = new CancellationTokenSource();
        var token = cts.Token;

        _sessionRepositoryMock
            .Setup(r => r.FindByGameIdAsync(gameId, token))
            .ReturnsAsync(new List<GameSession>());

        // Act
        await _handler.Handle(query, token);

        // Assert
        _sessionRepositoryMock.Verify(r => r.FindByGameIdAsync(gameId, token), Times.Once);
    }

    [Fact]
    public async Task Handle_WithLastPage_ReturnsRemainingItems()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var sessions = Enumerable.Range(1, 10).Select(_ => CreateSession(gameId)).ToList();
        var query = new GetGameSessionsQuery(gameId, PageNumber: 4, PageSize: 3);

        _sessionRepositoryMock
            .Setup(r => r.FindByGameIdAsync(gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(sessions);

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Single(result); // Page 4 with size 3 should return 1 item (item 10)
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
