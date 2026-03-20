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
/// Comprehensive tests for GetSessionHistoryQueryHandler.
/// Tests session history retrieval with filtering, pagination, and validation.
/// </summary>
[Trait("Category", TestCategories.Unit)]
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
        result.Should().NotBeNull();
        result.Count.Should().Be(2);
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
        result.Should().NotBeNull();
        result.Should().ContainSingle();
        result[0].GameId.Should().Be(gameId);
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
        result.Should().NotBeNull();
        result.Should().ContainSingle();
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
        result.Should().NotBeNull();
        result.Count.Should().Be(2);
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
        result.Should().NotBeNull();
        result.Should().ContainSingle();
        result[0].GameId.Should().Be(gameId);
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
        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }
    [Fact]
    public async Task Handle_WithNegativeLimit_ThrowsArgumentException()
    {
        // Arrange
        var query = new GetSessionHistoryQuery(Limit: -1);

        // Act & Assert
        var act = 
            () => _handler.Handle(query, TestContext.Current.CancellationToken);
        var exception = (await act.Should().ThrowAsync<ArgumentException>()).Which;

        exception.Message.Should().ContainEquivalentOf("Limit must be non-negative");
    }

    [Fact]
    public async Task Handle_WithLimitExceeding1000_ThrowsArgumentException()
    {
        // Arrange
        var query = new GetSessionHistoryQuery(Limit: 1001);

        // Act & Assert
        var act = 
            () => _handler.Handle(query, TestContext.Current.CancellationToken);
        var exception = (await act.Should().ThrowAsync<ArgumentException>()).Which;

        exception.Message.Should().ContainEquivalentOf("Limit cannot exceed 1000");
    }

    [Fact]
    public async Task Handle_WithNegativeOffset_ThrowsArgumentException()
    {
        // Arrange
        var query = new GetSessionHistoryQuery(Offset: -1);

        // Act & Assert
        var act = 
            () => _handler.Handle(query, TestContext.Current.CancellationToken);
        var exception = (await act.Should().ThrowAsync<ArgumentException>()).Which;

        exception.Message.Should().ContainEquivalentOf("Offset must be non-negative");
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
        result.Should().NotBeNull();
        result.Should().ContainSingle();
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
        result.Should().NotBeNull();
        result.Should().ContainSingle();
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
        result.Should().NotBeNull();
        result.Should().ContainSingle();

        var dto = result[0];
        dto.Id.Should().Be(session.Id);
        dto.GameId.Should().Be(gameId);
        dto.Status.Should().Be("Completed");
        dto.WinnerName.Should().Be("Alice");
        dto.PlayerCount.Should().Be(session.Players.Count);
        dto.Players.Count.Should().Be(session.Players.Count);
    }
}

