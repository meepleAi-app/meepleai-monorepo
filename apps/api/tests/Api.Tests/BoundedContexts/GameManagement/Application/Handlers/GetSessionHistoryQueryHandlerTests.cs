using Api.BoundedContexts.GameManagement.Application.Handlers;
using Api.BoundedContexts.GameManagement.Application.Queries;
using Api.BoundedContexts.GameManagement.Domain.Entities;
using Api.BoundedContexts.GameManagement.Domain.Repositories;
using Api.Tests.BoundedContexts.GameManagement.TestHelpers;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.GameManagement.Application.Handlers;

/// <summary>
/// Comprehensive tests for GetSessionHistoryQueryHandler.
/// Tests session history retrieval with filtering, pagination, and validation.
/// </summary>
public class GetSessionHistoryQueryHandlerTests
{
    private readonly Mock<IGameSessionRepository> _sessionRepositoryMock;
    private readonly GetSessionHistoryQueryHandler _handler;

    public GetSessionHistoryQueryHandlerTests()
    {
        _sessionRepositoryMock = new Mock<IGameSessionRepository>();
        _handler = new GetSessionHistoryQueryHandler(_sessionRepositoryMock.Object);
    }
    [Fact]
    public async Task Handle_WithNoFilters_ReturnsAllHistoricalSessions()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var sessions = new List<GameSession>
        {
            new GameSessionBuilder()
                .WithGameId(gameId)
                .WithCompletedStatus()
                .Build(),
            new GameSessionBuilder()
                .WithGameId(gameId)
                .WithAbandonedStatus()
                .Build()
        };

        _sessionRepositoryMock
            .Setup(r => r.FindHistoryAsync(
                null, null, null, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(sessions);

        var query = new GetSessionHistoryQuery();

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.Count);
        _sessionRepositoryMock.Verify(
            r => r.FindHistoryAsync(null, null, null, null, null, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WithGameIdFilter_PassesFilterToRepository()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var sessions = new List<GameSession>
        {
            new GameSessionBuilder()
                .WithGameId(gameId)
                .WithCompletedStatus()
                .Build()
        };

        _sessionRepositoryMock
            .Setup(r => r.FindHistoryAsync(
                gameId, null, null, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(sessions);

        var query = new GetSessionHistoryQuery(GameId: gameId);

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Single(result);
        Assert.Equal(gameId, result[0].GameId);
        _sessionRepositoryMock.Verify(
            r => r.FindHistoryAsync(gameId, null, null, null, null, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WithDateRangeFilters_PassesFiltersToRepository()
    {
        // Arrange
        var startDate = new DateTime(2024, 1, 1);
        var endDate = new DateTime(2024, 12, 31);
        var sessions = new List<GameSession>
        {
            new GameSessionBuilder()
                .WithCompletedStatus()
                .Build()
        };

        _sessionRepositoryMock
            .Setup(r => r.FindHistoryAsync(
                null, startDate, endDate, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(sessions);

        var query = new GetSessionHistoryQuery(
            StartDate: startDate,
            EndDate: endDate);

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Single(result);
        _sessionRepositoryMock.Verify(
            r => r.FindHistoryAsync(null, startDate, endDate, null, null, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WithPagination_PassesPaginationToRepository()
    {
        // Arrange
        var limit = 10;
        var offset = 20;
        var sessions = new List<GameSession>
        {
            new GameSessionBuilder().WithCompletedStatus().Build(),
            new GameSessionBuilder().WithCompletedStatus().Build()
        };

        _sessionRepositoryMock
            .Setup(r => r.FindHistoryAsync(
                null, null, null, limit, offset, It.IsAny<CancellationToken>()))
            .ReturnsAsync(sessions);

        var query = new GetSessionHistoryQuery(
            Limit: limit,
            Offset: offset);

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.Count);
        _sessionRepositoryMock.Verify(
            r => r.FindHistoryAsync(null, null, null, limit, offset, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WithAllFilters_PassesAllFiltersToRepository()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var startDate = new DateTime(2024, 6, 1);
        var endDate = new DateTime(2024, 6, 30);
        var limit = 5;
        var offset = 0;

        var sessions = new List<GameSession>
        {
            new GameSessionBuilder()
                .WithGameId(gameId)
                .WithCompletedStatus()
                .Build()
        };

        _sessionRepositoryMock
            .Setup(r => r.FindHistoryAsync(
                gameId, startDate, endDate, limit, offset, It.IsAny<CancellationToken>()))
            .ReturnsAsync(sessions);

        var query = new GetSessionHistoryQuery(
            GameId: gameId,
            StartDate: startDate,
            EndDate: endDate,
            Limit: limit,
            Offset: offset);

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Single(result);
        Assert.Equal(gameId, result[0].GameId);
        _sessionRepositoryMock.Verify(
            r => r.FindHistoryAsync(gameId, startDate, endDate, limit, offset, It.IsAny<CancellationToken>()),
            Times.Once);
    }
    [Fact]
    public async Task Handle_WithEmptyResult_ReturnsEmptyList()
    {
        // Arrange
        _sessionRepositoryMock
            .Setup(r => r.FindHistoryAsync(
                null, null, null, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<GameSession>());

        var query = new GetSessionHistoryQuery();

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result);
    }
    [Fact]
    public async Task Handle_WithNegativeLimit_ThrowsArgumentException()
    {
        // Arrange
        var query = new GetSessionHistoryQuery(Limit: -1);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(
            () => _handler.Handle(query, TestContext.Current.CancellationToken));

        Assert.Contains("Limit must be non-negative", exception.Message);
    }

    [Fact]
    public async Task Handle_WithLimitExceeding1000_ThrowsArgumentException()
    {
        // Arrange
        var query = new GetSessionHistoryQuery(Limit: 1001);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(
            () => _handler.Handle(query, TestContext.Current.CancellationToken));

        Assert.Contains("Limit cannot exceed 1000", exception.Message);
    }

    [Fact]
    public async Task Handle_WithNegativeOffset_ThrowsArgumentException()
    {
        // Arrange
        var query = new GetSessionHistoryQuery(Offset: -1);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(
            () => _handler.Handle(query, TestContext.Current.CancellationToken));

        Assert.Contains("Offset must be non-negative", exception.Message);
    }

    [Fact]
    public async Task Handle_WithValidLimit1000_Succeeds()
    {
        // Arrange
        var sessions = new List<GameSession>
        {
            new GameSessionBuilder().WithCompletedStatus().Build()
        };

        _sessionRepositoryMock
            .Setup(r => r.FindHistoryAsync(
                null, null, null, 1000, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(sessions);

        var query = new GetSessionHistoryQuery(Limit: 1000);

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Single(result);
    }

    [Fact]
    public async Task Handle_WithZeroLimitAndOffset_Succeeds()
    {
        // Arrange
        var sessions = new List<GameSession>
        {
            new GameSessionBuilder().WithCompletedStatus().Build()
        };

        _sessionRepositoryMock
            .Setup(r => r.FindHistoryAsync(
                null, null, null, 0, 0, It.IsAny<CancellationToken>()))
            .ReturnsAsync(sessions);

        var query = new GetSessionHistoryQuery(Limit: 0, Offset: 0);

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Single(result);
    }
    [Fact]
    public async Task Handle_MapsSessionsToDtosCorrectly()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var startedAt = DateTime.UtcNow.AddHours(-2);
        var completedAt = DateTime.UtcNow;

        var session = new GameSessionBuilder()
            .WithGameId(gameId)
            .WithCompletedStatus()
            .WithWinner("Alice")
            .Build();

        _sessionRepositoryMock
            .Setup(r => r.FindHistoryAsync(
                null, null, null, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<GameSession> { session });

        var query = new GetSessionHistoryQuery();

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Single(result);

        var dto = result[0];
        Assert.Equal(session.Id, dto.Id);
        Assert.Equal(gameId, dto.GameId);
        Assert.Equal("Completed", dto.Status);
        Assert.Equal("Alice", dto.WinnerName);
        Assert.Equal(session.Players.Count, dto.PlayerCount);
        Assert.Equal(session.Players.Count, dto.Players.Count);
    }
}

