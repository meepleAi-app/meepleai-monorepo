using Api.BoundedContexts.Administration.Application.DTOs;
using Api.BoundedContexts.Administration.Application.Queries;
using Api.Tests.Constants;
using Xunit;

namespace Api.Tests.BoundedContexts.Administration.Application.Queries.Unit;

/// <summary>
/// Unit tests for GetDashboardQuery and related DTOs.
/// Issue #3972: Dashboard Aggregated API Endpoint.
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
        var userId = Guid.NewGuid();
        var user = new DashboardUserDto(userId, "Test User", "test@example.com");
        var stats = new DashboardUserStatsDto(10, 5, 15, 3, 7);
        var sessions = new List<DashboardSessionDto>();
        var library = new DashboardLibrarySnapshotDto(
            new DashboardQuotaDto(50, 1000),
            new List<DashboardTopGameDto>()
        );
        var activity = new List<DashboardActivityDto>();
        var chats = new List<DashboardChatDto>();

        // Act
        var response = new DashboardResponseDto(user, stats, sessions, library, activity, chats);

        // Assert
        Assert.NotNull(response);
        Assert.Equal(userId, response.User.Id);
        Assert.Equal("Test User", response.User.Username);
        Assert.Equal("test@example.com", response.User.Email);
        Assert.Equal(10, response.Stats.LibraryCount);
        Assert.Equal(5, response.Stats.PlayedLast30Days);
        Assert.Equal(15, response.Stats.ChatCount);
        Assert.Equal(3, response.Stats.WishlistCount);
        Assert.Equal(7, response.Stats.CurrentStreak);
        Assert.Equal(50, response.LibrarySnapshot.Quota.Used);
    }

    [Fact]
    public void DashboardUserStatsDto_WithAllFields_ReturnsCorrectValues()
    {
        // Arrange & Act
        var stats = new DashboardUserStatsDto(
            LibraryCount: 100,
            PlayedLast30Days: 15,
            ChatCount: 20,
            WishlistCount: 5,
            CurrentStreak: 7
        );

        // Assert
        Assert.Equal(100, stats.LibraryCount);
        Assert.Equal(15, stats.PlayedLast30Days);
        Assert.Equal(20, stats.ChatCount);
        Assert.Equal(5, stats.WishlistCount);
        Assert.Equal(7, stats.CurrentStreak);
    }

    [Fact]
    public void DashboardQuotaDto_WithValues_ReturnsCorrectValues()
    {
        // Arrange & Act
        var quota = new DashboardQuotaDto(Used: 250, Total: 1000);

        // Assert
        Assert.Equal(250, quota.Used);
        Assert.Equal(1000, quota.Total);
    }

    [Fact]
    public void DashboardSessionDto_WithAllFields_CreatesSession()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var lastActivity = DateTime.UtcNow;
        var players = new DashboardPlayersDto(Current: 3, Total: 4);
        var progress = new DashboardProgressDto(Turn: 5, Duration: "120min");

        // Act
        var session = new DashboardSessionDto(
            Id: sessionId,
            GameName: "Chess",
            GameId: gameId,
            CoverUrl: "https://example.com/chess.jpg",
            Players: players,
            Progress: progress,
            LastActivity: lastActivity
        );

        // Assert
        Assert.Equal(sessionId, session.Id);
        Assert.Equal("Chess", session.GameName);
        Assert.Equal(gameId, session.GameId);
        Assert.Equal("https://example.com/chess.jpg", session.CoverUrl);
        Assert.Equal(3, session.Players.Current);
        Assert.Equal(4, session.Players.Total);
        Assert.Equal(5, session.Progress.Turn);
        Assert.Equal("120min", session.Progress.Duration);
        Assert.Equal(lastActivity, session.LastActivity);
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
    public void DashboardActivityDto_WithGameEvent_CreatesActivity()
    {
        // Arrange
        var timestamp = DateTime.UtcNow;
        var gameId = Guid.NewGuid().ToString();

        // Act
        var activity = new DashboardActivityDto(
            Id: "game-123",
            Type: "game_added",
            GameId: gameId,
            GameName: "Catan",
            SessionId: null,
            ChatId: null,
            Topic: null,
            Timestamp: timestamp
        );

        // Assert
        Assert.Equal("game-123", activity.Id);
        Assert.Equal("game_added", activity.Type);
        Assert.Equal(gameId, activity.GameId);
        Assert.Equal("Catan", activity.GameName);
        Assert.Null(activity.SessionId);
        Assert.Null(activity.ChatId);
        Assert.Null(activity.Topic);
        Assert.Equal(timestamp, activity.Timestamp);
    }

    [Fact]
    public void DashboardActivityDto_WithChatEvent_CreatesActivity()
    {
        // Arrange
        var timestamp = DateTime.UtcNow;
        var chatId = Guid.NewGuid().ToString();

        // Act
        var activity = new DashboardActivityDto(
            Id: "chat-456",
            Type: "chat_saved",
            GameId: null,
            GameName: null,
            SessionId: null,
            ChatId: chatId,
            Topic: "Rules discussion",
            Timestamp: timestamp
        );

        // Assert
        Assert.Equal("chat-456", activity.Id);
        Assert.Equal("chat_saved", activity.Type);
        Assert.Null(activity.GameId);
        Assert.Null(activity.GameName);
        Assert.Equal(chatId, activity.ChatId);
        Assert.Equal("Rules discussion", activity.Topic);
    }

    [Fact]
    public void DashboardChatDto_WithChat_CreatesChat()
    {
        // Arrange
        var timestamp = DateTime.UtcNow;

        // Act
        var chat = new DashboardChatDto(
            Id: "chat-789",
            Topic: "Rules discussion",
            MessageCount: 8,
            Timestamp: timestamp
        );

        // Assert
        Assert.Equal("chat-789", chat.Id);
        Assert.Equal("Rules discussion", chat.Topic);
        Assert.Equal(8, chat.MessageCount);
        Assert.Equal(timestamp, chat.Timestamp);
    }

    [Fact]
    public void DashboardChatDto_WithNullMessageCount_AllowsNull()
    {
        // Arrange & Act
        var chat = new DashboardChatDto(
            Id: "chat-123",
            Topic: "General chat",
            MessageCount: null,
            Timestamp: DateTime.UtcNow
        );

        // Assert
        Assert.Null(chat.MessageCount);
    }

    [Fact]
    public void DashboardLibrarySnapshotDto_WithEmptyTopGames_HasEmptyList()
    {
        // Arrange & Act
        var snapshot = new DashboardLibrarySnapshotDto(
            Quota: new DashboardQuotaDto(0, 1000),
            TopGames: new List<DashboardTopGameDto>()
        );

        // Assert
        Assert.Empty(snapshot.TopGames);
        Assert.Equal(0, snapshot.Quota.Used);
    }

    [Fact]
    public void DashboardProgressDto_WithValues_ReturnsCorrectValues()
    {
        // Arrange & Act
        var progress = new DashboardProgressDto(Turn: 12, Duration: "45min");

        // Assert
        Assert.Equal(12, progress.Turn);
        Assert.Equal("45min", progress.Duration);
    }

    [Fact]
    public void DashboardUserDto_WithAllFields_ReturnsCorrectValues()
    {
        // Arrange
        var userId = Guid.NewGuid();

        // Act
        var user = new DashboardUserDto(
            Id: userId,
            Username: "Marco",
            Email: "marco@example.com"
        );

        // Assert
        Assert.Equal(userId, user.Id);
        Assert.Equal("Marco", user.Username);
        Assert.Equal("marco@example.com", user.Email);
    }
}
