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
        result.Should().NotBeNull();
        result.TotalSessions.Should().Be(3);
        result.CompletedSessions.Should().Be(2);
        result.AbandonedSessions.Should().Be(1);
        result.AverageDurationMinutes.Should().Be(60); // (60 + 90 + 30) / 3 = 60
        result.TopPlayers.Count.Should().Be(2);
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
        result.Should().NotBeNull();
        result.CompletedSessions.Should().Be(4);
        result.TopPlayers.Count.Should().Be(3);

        var alice = result.TopPlayers.First(p => p.PlayerName == "Alice");
        alice.WinCount.Should().Be(2);
        alice.WinRate.Should().Be(50.00m); // 2/4 * 100 = 50%

        var bob = result.TopPlayers.First(p => p.PlayerName == "Bob");
        bob.WinCount.Should().Be(1);
        bob.WinRate.Should().Be(25.00m); // 1/4 * 100 = 25%
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
        result.Should().NotBeNull();
        result.TopPlayers.Count.Should().Be(3);
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
        result.Should().NotBeNull();
        result.TopPlayers.Count.Should().Be(3);
        result.TopPlayers[0].PlayerName.Should().Be("Alice");
        result.TopPlayers[0].WinCount.Should().Be(3);
        result.TopPlayers[1].PlayerName.Should().Be("Bob");
        result.TopPlayers[1].WinCount.Should().Be(2);
        result.TopPlayers[2].PlayerName.Should().Be("Charlie");
        result.TopPlayers[2].WinCount.Should().Be(1);
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
        result.Should().NotBeNull();
        result.TotalSessions.Should().Be(0);
        result.CompletedSessions.Should().Be(0);
        result.AbandonedSessions.Should().Be(0);
        result.AverageDurationMinutes.Should().Be(0);
        result.TopPlayers.Should().BeEmpty();
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
        result.Should().NotBeNull();
        result.TotalSessions.Should().Be(2);
        result.CompletedSessions.Should().Be(0);
        result.AbandonedSessions.Should().Be(2);
        result.AverageDurationMinutes.Should().Be(38); // (30 + 45) / 2 = 37.5 rounded to 38
        result.TopPlayers.Should().BeEmpty(); // No winners in abandoned sessions
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
        result.Should().NotBeNull();
        result.CompletedSessions.Should().Be(3);
        result.TopPlayers.Count.Should().Be(2); // Only Alice and Bob
        result.TopPlayers[0].PlayerName.Should().Be("Alice");
        result.TopPlayers[1].PlayerName.Should().Be("Bob");
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
        result.Should().NotBeNull();
        result.CompletedSessions.Should().Be(3);
        // Case-insensitive aggregation: "Alice", "alice", and " Alice " should be treated as the same player
        result.TopPlayers.Should().ContainSingle();
        result.TopPlayers[0].PlayerName.Should().Be("Alice"); // First occurrence casing preserved
        result.TopPlayers[0].WinCount.Should().Be(3);
        result.TopPlayers[0].WinRate.Should().Be(100.00m); // 3/3 * 100 = 100%
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
        result.Should().NotBeNull();
        result.TotalSessions.Should().Be(1);
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
        result.Should().NotBeNull();
        result.TotalSessions.Should().Be(1);
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
        result.Should().NotBeNull();
        result.AverageDurationMinutes.Should().Be(75);
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
        result.Should().NotBeNull();
        result.AverageDurationMinutes.Should().Be(62); // (61 + 62 + 63) / 3 = 62
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
        result.Should().NotBeNull();
        var alice = result.TopPlayers.First(p => p.PlayerName == "Alice");
        alice.WinRate.Should().Be(33.33m); // 1/3 * 100 = 33.33%
    }
}

