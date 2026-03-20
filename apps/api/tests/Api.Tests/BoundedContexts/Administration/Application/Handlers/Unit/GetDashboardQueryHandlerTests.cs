using Api.BoundedContexts.Administration.Application.DTOs;
using Api.BoundedContexts.Administration.Application.Queries;
using Api.Tests.Constants;
using Xunit;
using FluentAssertions;

namespace Api.Tests.BoundedContexts.Administration.Application.Handlers.Unit;

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
        query.UserId.Should().Be(userId);
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
        response.Should().NotBeNull();
        response.User.Id.Should().Be(userId);
        response.User.Username.Should().Be("Test User");
        response.User.Email.Should().Be("test@example.com");
        response.Stats.LibraryCount.Should().Be(10);
        response.Stats.PlayedLast30Days.Should().Be(5);
        response.Stats.ChatCount.Should().Be(15);
        response.Stats.WishlistCount.Should().Be(3);
        response.Stats.CurrentStreak.Should().Be(7);
        response.LibrarySnapshot.Quota.Used.Should().Be(50);
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
        stats.LibraryCount.Should().Be(100);
        stats.PlayedLast30Days.Should().Be(15);
        stats.ChatCount.Should().Be(20);
        stats.WishlistCount.Should().Be(5);
        stats.CurrentStreak.Should().Be(7);
    }

    [Fact]
    public void DashboardQuotaDto_WithValues_ReturnsCorrectValues()
    {
        // Arrange & Act
        var quota = new DashboardQuotaDto(Used: 250, Total: 1000);

        // Assert
        quota.Used.Should().Be(250);
        quota.Total.Should().Be(1000);
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
        session.Id.Should().Be(sessionId);
        session.GameName.Should().Be("Chess");
        session.GameId.Should().Be(gameId);
        session.CoverUrl.Should().Be("https://example.com/chess.jpg");
        session.Players.Current.Should().Be(3);
        session.Players.Total.Should().Be(4);
        session.Progress.Turn.Should().Be(5);
        session.Progress.Duration.Should().Be("120min");
        session.LastActivity.Should().Be(lastActivity);
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
        topGame.Id.Should().Be(gameId);
        topGame.Title.Should().Be("Catan");
        topGame.CoverUrl.Should().Be("https://example.com/catan.jpg");
        topGame.Rating.Should().Be(4.5m);
        topGame.PlayCount.Should().Be(25);
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
        activity.Id.Should().Be("game-123");
        activity.Type.Should().Be("game_added");
        activity.GameId.Should().Be(gameId);
        activity.GameName.Should().Be("Catan");
        activity.SessionId.Should().BeNull();
        activity.ChatId.Should().BeNull();
        activity.Topic.Should().BeNull();
        activity.Timestamp.Should().Be(timestamp);
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
        activity.Id.Should().Be("chat-456");
        activity.Type.Should().Be("chat_saved");
        activity.GameId.Should().BeNull();
        activity.GameName.Should().BeNull();
        activity.ChatId.Should().Be(chatId);
        activity.Topic.Should().Be("Rules discussion");
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
        chat.Id.Should().Be("chat-789");
        chat.Topic.Should().Be("Rules discussion");
        chat.MessageCount.Should().Be(8);
        chat.Timestamp.Should().Be(timestamp);
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
        chat.MessageCount.Should().BeNull();
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
        snapshot.TopGames.Should().BeEmpty();
        snapshot.Quota.Used.Should().Be(0);
    }

    [Fact]
    public void DashboardProgressDto_WithValues_ReturnsCorrectValues()
    {
        // Arrange & Act
        var progress = new DashboardProgressDto(Turn: 12, Duration: "45min");

        // Assert
        progress.Turn.Should().Be(12);
        progress.Duration.Should().Be("45min");
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
        user.Id.Should().Be(userId);
        user.Username.Should().Be("Marco");
        user.Email.Should().Be("marco@example.com");
    }
}