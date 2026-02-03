using Api.BoundedContexts.Administration.Application.DTOs;
using Api.BoundedContexts.Administration.Application.Queries;
using Api.Tests.Constants;
using Xunit;

namespace Api.Tests.BoundedContexts.Administration.Application.Handlers.Unit;

/// <summary>
/// Unit tests for GetDashboardQuery and related DTOs.
/// Issue #3314: User Dashboard Aggregated API.
///
/// Note: Full handler testing is deferred to integration tests due to HybridCache
/// non-mockable design. Unit tests here focus on DTOs and query structure.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "Administration")]
public sealed class GetDashboardQueryTests
{
    [Fact]
    public void GetDashboardQuery_WithValidUserId_CreatesQuery()
    {
        // Arrange
        var userId = Guid.NewGuid();

        // Act
        var query = new GetDashboardQuery(userId);

        // Assert
        Assert.Equal(userId, query.UserId);
    }

    [Fact]
    public void DashboardResponseDto_WithAllData_CreatesResponse()
    {
        // Arrange
        var user = new DashboardUserDto("Test User", DateTime.UtcNow);
        var stats = new DashboardStatsResponseDto(
            new DashboardStatItemDto(10, 2),
            new DashboardPlayedStatDto(5, 3),
            new DashboardStatCountDto(15),
            new DashboardStatItemDto(3, 1)
        );
        var sessions = new List<DashboardSessionDto>();
        var library = new DashboardLibrarySnapshotDto(
            new DashboardQuotaDto(50, 1000, 5),
            new List<DashboardTopGameDto>()
        );
        var activity = new List<DashboardActivityDto>();
        var chats = new List<DashboardChatDto>();

        // Act
        var response = new DashboardResponseDto(user, stats, sessions, library, activity, chats);

        // Assert
        Assert.NotNull(response);
        Assert.Equal("Test User", response.User.Name);
        Assert.Equal(10, response.Stats.Collection.Total);
        Assert.Equal(5, response.Stats.Played.Total);
        Assert.Equal(15, response.Stats.Chats.Total);
        Assert.Equal(50, response.LibrarySnapshot.Quota.Used);
    }

    [Fact]
    public void DashboardStatItemDto_WithTrendCalculation_ReturnsTrend()
    {
        // Arrange & Act
        var statItem = new DashboardStatItemDto(Total: 100, Trend: 15);

        // Assert
        Assert.Equal(100, statItem.Total);
        Assert.Equal(15, statItem.Trend);
    }

    [Fact]
    public void DashboardPlayedStatDto_WithStreak_ReturnsStreak()
    {
        // Arrange & Act
        var playedStat = new DashboardPlayedStatDto(Total: 50, Streak: 7);

        // Assert
        Assert.Equal(50, playedStat.Total);
        Assert.Equal(7, playedStat.Streak);
    }

    [Fact]
    public void DashboardQuotaDto_WithPercentage_ReturnsCorrectValues()
    {
        // Arrange & Act
        var quota = new DashboardQuotaDto(Used: 250, Total: 1000, Percentage: 25);

        // Assert
        Assert.Equal(250, quota.Used);
        Assert.Equal(1000, quota.Total);
        Assert.Equal(25, quota.Percentage);
    }

    [Fact]
    public void DashboardSessionDto_WithAllFields_CreatesSession()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var startDate = DateTime.UtcNow;
        var players = new DashboardPlayersDto(Current: 3, Max: 4);

        // Act
        var session = new DashboardSessionDto(
            Id: sessionId,
            GameName: "Chess",
            GameId: gameId,
            StartDate: startDate,
            Players: players,
            Turn: 5,
            Duration: 120
        );

        // Assert
        Assert.Equal(sessionId, session.Id);
        Assert.Equal("Chess", session.GameName);
        Assert.Equal(gameId, session.GameId);
        Assert.Equal(startDate, session.StartDate);
        Assert.Equal(3, session.Players.Current);
        Assert.Equal(4, session.Players.Max);
        Assert.Equal(5, session.Turn);
        Assert.Equal(120, session.Duration);
    }

    [Fact]
    public void DashboardTopGameDto_WithGameData_CreatesTopGame()
    {
        // Arrange
        var gameId = Guid.NewGuid();

        // Act
        var topGame = new DashboardTopGameDto(
            Id: gameId,
            Title: "Catan",
            CoverUrl: "https://example.com/catan.jpg",
            Rating: 4.5m,
            PlayCount: 25
        );

        // Assert
        Assert.Equal(gameId, topGame.Id);
        Assert.Equal("Catan", topGame.Title);
        Assert.Equal("https://example.com/catan.jpg", topGame.CoverUrl);
        Assert.Equal(4.5m, topGame.Rating);
        Assert.Equal(25, topGame.PlayCount);
    }

    [Fact]
    public void DashboardActivityDto_WithEvent_CreatesActivity()
    {
        // Arrange
        var timestamp = DateTime.UtcNow;

        // Act
        var activity = new DashboardActivityDto(
            Id: "game-123",
            Type: "game_added",
            Title: "Added Catan",
            EntityId: "456",
            Timestamp: timestamp
        );

        // Assert
        Assert.Equal("game-123", activity.Id);
        Assert.Equal("game_added", activity.Type);
        Assert.Equal("Added Catan", activity.Title);
        Assert.Equal("456", activity.EntityId);
        Assert.Equal(timestamp, activity.Timestamp);
    }

    [Fact]
    public void DashboardChatDto_WithChat_CreatesChat()
    {
        // Arrange
        var lastMessageAt = DateTime.UtcNow;

        // Act
        var chat = new DashboardChatDto(
            Id: "chat-789",
            Title: "Rules discussion",
            LastMessageAt: lastMessageAt
        );

        // Assert
        Assert.Equal("chat-789", chat.Id);
        Assert.Equal("Rules discussion", chat.Title);
        Assert.Equal(lastMessageAt, chat.LastMessageAt);
    }

    [Fact]
    public void DashboardLibrarySnapshotDto_WithEmptyTopGames_HasEmptyList()
    {
        // Arrange & Act
        var snapshot = new DashboardLibrarySnapshotDto(
            Quota: new DashboardQuotaDto(0, 1000, 0),
            TopGames: new List<DashboardTopGameDto>()
        );

        // Assert
        Assert.Empty(snapshot.TopGames);
        Assert.Equal(0, snapshot.Quota.Used);
    }
}
