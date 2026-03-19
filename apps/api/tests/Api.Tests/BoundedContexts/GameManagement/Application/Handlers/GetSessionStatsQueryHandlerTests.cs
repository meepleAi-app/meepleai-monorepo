using Api.BoundedContexts.GameManagement.Application.Commands;
using Api.BoundedContexts.GameManagement.Application.Queries;
using Api.BoundedContexts.GameManagement.Application.Queries;
using Api.BoundedContexts.GameManagement.Domain.Entities;
using Api.BoundedContexts.GameManagement.Domain.Repositories;
using Api.Tests.BoundedContexts.GameManagement.TestHelpers;
using Moq;
using Xunit;
using Api.Tests.Constants;

namespace Api.Tests.BoundedContexts.GameManagement.Application.Handlers;

/// <summary>
/// Comprehensive tests for GetSessionStatsQueryHandler.
/// Tests session statistics aggregation, filtering, and win rate calculations.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class GetSessionStatsQueryHandlerTests
{
    private readonly Mock<IGameSessionRepository> _sessionRepositoryMock;
    private readonly GetSessionStatsQueryHandler _handler;

    public GetSessionStatsQueryHandlerTests()
    {
        _sessionRepositoryMock = new Mock<IGameSessionRepository>();
        _handler = new GetSessionStatsQueryHandler(_sessionRepositoryMock.Object);
    }
    [Fact]
    public async Task Handle_WithMultipleSessions_ReturnsAggregatedStatistics()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var sessions = new List<GameSession>
        {
            new GameSessionBuilder()
                .WithGameId(gameId)
                .WithCompletedStatus()
                .WithWinner("Alice")
                .WithDuration(60)
                .Build(),
            new GameSessionBuilder()
                .WithGameId(gameId)
                .WithCompletedStatus()
                .WithWinner("Bob")
                .WithDuration(90)
                .Build(),
            new GameSessionBuilder()
                .WithGameId(gameId)
                .WithAbandonedStatus()
                .WithDuration(30)
                .Build()
        };

        _sessionRepositoryMock
            .Setup(r => r.FindHistoryAsync(
                null, null, null, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(sessions);

        var query = new GetSessionStatsQuery();

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(3, result.TotalSessions);
        Assert.Equal(2, result.CompletedSessions);
        Assert.Equal(1, result.AbandonedSessions);
        Assert.Equal(60, result.AverageDurationMinutes); // (60 + 90 + 30) / 3 = 60
        Assert.Equal(2, result.TopPlayers.Count);
    }

    [Fact]
    public async Task Handle_CalculatesWinRatesCorrectly()
    {
        // Arrange
        var sessions = new List<GameSession>
        {
            new GameSessionBuilder().WithCompletedStatus().WithWinner("Alice").Build(),
            new GameSessionBuilder().WithCompletedStatus().WithWinner("Alice").Build(),
            new GameSessionBuilder().WithCompletedStatus().WithWinner("Bob").Build(),
            new GameSessionBuilder().WithCompletedStatus().WithWinner("Charlie").Build()
        };

        _sessionRepositoryMock
            .Setup(r => r.FindHistoryAsync(
                null, null, null, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(sessions);

        var query = new GetSessionStatsQuery();

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(4, result.CompletedSessions);
        Assert.Equal(3, result.TopPlayers.Count);

        var alice = result.TopPlayers.First(p => p.PlayerName == "Alice");
        Assert.Equal(2, alice.WinCount);
        Assert.Equal(50.00m, alice.WinRate); // 2/4 * 100 = 50%

        var bob = result.TopPlayers.First(p => p.PlayerName == "Bob");
        Assert.Equal(1, bob.WinCount);
        Assert.Equal(25.00m, bob.WinRate); // 1/4 * 100 = 25%
    }

    [Fact]
    public async Task Handle_RespectsTopPlayersLimit()
    {
        // Arrange
        var sessions = new List<GameSession>
        {
            new GameSessionBuilder().WithCompletedStatus().WithWinner("Alice").Build(),
            new GameSessionBuilder().WithCompletedStatus().WithWinner("Bob").Build(),
            new GameSessionBuilder().WithCompletedStatus().WithWinner("Charlie").Build(),
            new GameSessionBuilder().WithCompletedStatus().WithWinner("Dave").Build(),
            new GameSessionBuilder().WithCompletedStatus().WithWinner("Eve").Build(),
            new GameSessionBuilder().WithCompletedStatus().WithWinner("Frank").Build()
        };

        _sessionRepositoryMock
            .Setup(r => r.FindHistoryAsync(
                null, null, null, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(sessions);

        var query = new GetSessionStatsQuery(TopPlayersLimit: 3);

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(3, result.TopPlayers.Count);
    }

    [Fact]
    public async Task Handle_OrdersTopPlayersByWinCount()
    {
        // Arrange
        var sessions = new List<GameSession>
        {
            new GameSessionBuilder().WithCompletedStatus().WithWinner("Alice").Build(),
            new GameSessionBuilder().WithCompletedStatus().WithWinner("Alice").Build(),
            new GameSessionBuilder().WithCompletedStatus().WithWinner("Alice").Build(),
            new GameSessionBuilder().WithCompletedStatus().WithWinner("Bob").Build(),
            new GameSessionBuilder().WithCompletedStatus().WithWinner("Bob").Build(),
            new GameSessionBuilder().WithCompletedStatus().WithWinner("Charlie").Build()
        };

        _sessionRepositoryMock
            .Setup(r => r.FindHistoryAsync(
                null, null, null, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(sessions);

        var query = new GetSessionStatsQuery();

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(3, result.TopPlayers.Count);
        Assert.Equal("Alice", result.TopPlayers[0].PlayerName);
        Assert.Equal(3, result.TopPlayers[0].WinCount);
        Assert.Equal("Bob", result.TopPlayers[1].PlayerName);
        Assert.Equal(2, result.TopPlayers[1].WinCount);
        Assert.Equal("Charlie", result.TopPlayers[2].PlayerName);
        Assert.Equal(1, result.TopPlayers[2].WinCount);
    }
    [Fact]
    public async Task Handle_WithNoSessions_ReturnsZeroStatistics()
    {
        // Arrange
        _sessionRepositoryMock
            .Setup(r => r.FindHistoryAsync(
                null, null, null, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<GameSession>());

        var query = new GetSessionStatsQuery();

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(0, result.TotalSessions);
        Assert.Equal(0, result.CompletedSessions);
        Assert.Equal(0, result.AbandonedSessions);
        Assert.Equal(0, result.AverageDurationMinutes);
        Assert.Empty(result.TopPlayers);
    }

    [Fact]
    public async Task Handle_WithOnlyAbandonedSessions_ReturnsCorrectStatistics()
    {
        // Arrange
        var sessions = new List<GameSession>
        {
            new GameSessionBuilder().WithAbandonedStatus().WithDuration(30).Build(),
            new GameSessionBuilder().WithAbandonedStatus().WithDuration(45).Build()
        };

        _sessionRepositoryMock
            .Setup(r => r.FindHistoryAsync(
                null, null, null, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(sessions);

        var query = new GetSessionStatsQuery();

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.TotalSessions);
        Assert.Equal(0, result.CompletedSessions);
        Assert.Equal(2, result.AbandonedSessions);
        Assert.Equal(38, result.AverageDurationMinutes); // (30 + 45) / 2 = 37.5 rounded to 38
        Assert.Empty(result.TopPlayers); // No winners in abandoned sessions
    }

    [Fact]
    public async Task Handle_WithSessionsWithoutWinner_ExcludesFromWinStats()
    {
        // Arrange
        var sessions = new List<GameSession>
        {
            new GameSessionBuilder().WithCompletedStatus().WithWinner("Alice").Build(),
            new GameSessionBuilder().WithCompletedStatus().WithWinner(null).Build(), // No winner
            new GameSessionBuilder().WithCompletedStatus().WithWinner("Bob").Build()
        };

        _sessionRepositoryMock
            .Setup(r => r.FindHistoryAsync(
                null, null, null, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(sessions);

        var query = new GetSessionStatsQuery();

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(3, result.CompletedSessions);
        Assert.Equal(2, result.TopPlayers.Count); // Only Alice and Bob
        Assert.Equal("Alice", result.TopPlayers[0].PlayerName);
        Assert.Equal("Bob", result.TopPlayers[1].PlayerName);
    }

    [Fact]
    public async Task Handle_WithDuplicateWinnerNames_AggregatesCorrectly()
    {
        // Arrange
        var sessions = new List<GameSession>
        {
            new GameSessionBuilder().WithCompletedStatus().WithWinner("Alice").Build(),
            new GameSessionBuilder().WithCompletedStatus().WithWinner("alice").Build(), // Different case
            new GameSessionBuilder().WithCompletedStatus().WithWinner(" Alice ").Build() // Whitespace
        };

        _sessionRepositoryMock
            .Setup(r => r.FindHistoryAsync(
                null, null, null, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(sessions);

        var query = new GetSessionStatsQuery();

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(3, result.CompletedSessions);
        // Case-insensitive aggregation: "Alice", "alice", and " Alice " should be treated as the same player
        Assert.Single(result.TopPlayers);
        Assert.Equal("Alice", result.TopPlayers[0].PlayerName); // First occurrence casing preserved
        Assert.Equal(3, result.TopPlayers[0].WinCount);
        Assert.Equal(100.00m, result.TopPlayers[0].WinRate); // 3/3 * 100 = 100%
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

        var query = new GetSessionStatsQuery(GameId: gameId);

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(1, result.TotalSessions);
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
            new GameSessionBuilder().WithCompletedStatus().Build()
        };

        _sessionRepositoryMock
            .Setup(r => r.FindHistoryAsync(
                null, startDate, endDate, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(sessions);

        var query = new GetSessionStatsQuery(
            StartDate: startDate,
            EndDate: endDate);

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(1, result.TotalSessions);
        _sessionRepositoryMock.Verify(
            r => r.FindHistoryAsync(null, startDate, endDate, null, null, It.IsAny<CancellationToken>()),
            Times.Once);
    }
    [Fact]
    public async Task Handle_WithSingleSession_ReturnsExactDuration()
    {
        // Arrange
        var sessions = new List<GameSession>
        {
            new GameSessionBuilder()
                .WithCompletedStatus()
                .WithDuration(75)
                .Build()
        };

        _sessionRepositoryMock
            .Setup(r => r.FindHistoryAsync(
                null, null, null, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(sessions);

        var query = new GetSessionStatsQuery();

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(75, result.AverageDurationMinutes);
    }

    [Fact]
    public async Task Handle_RoundsAverageDurationToNearestMinute()
    {
        // Arrange
        var sessions = new List<GameSession>
        {
            new GameSessionBuilder().WithCompletedStatus().WithDuration(61).Build(),
            new GameSessionBuilder().WithCompletedStatus().WithDuration(62).Build(),
            new GameSessionBuilder().WithCompletedStatus().WithDuration(63).Build()
        };

        _sessionRepositoryMock
            .Setup(r => r.FindHistoryAsync(
                null, null, null, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(sessions);

        var query = new GetSessionStatsQuery();

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(62, result.AverageDurationMinutes); // (61 + 62 + 63) / 3 = 62
    }
    [Fact]
    public async Task Handle_RoundsWinRatesToTwoDecimalPlaces()
    {
        // Arrange
        var sessions = new List<GameSession>
        {
            new GameSessionBuilder().WithCompletedStatus().WithWinner("Alice").Build(),
            new GameSessionBuilder().WithCompletedStatus().WithWinner("Bob").Build(),
            new GameSessionBuilder().WithCompletedStatus().WithWinner("Charlie").Build()
        };

        _sessionRepositoryMock
            .Setup(r => r.FindHistoryAsync(
                null, null, null, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(sessions);

        var query = new GetSessionStatsQuery();

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        var alice = result.TopPlayers.First(p => p.PlayerName == "Alice");
        Assert.Equal(33.33m, alice.WinRate); // 1/3 * 100 = 33.33%
    }
}

