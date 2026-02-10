using Api.BoundedContexts.Administration.Application.DTOs;
using Api.BoundedContexts.Administration.Application.Services;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Infrastructure.Entities.SharedGameCatalog;
using Api.Infrastructure.Entities.UserLibrary;
using Api.Services;
using Api.Tests.Constants;
using Api.Tests.TestHelpers;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.Administration.Application.Services;

/// <summary>
/// Unit tests for ActivityTimelineService (Issue #3973).
/// Tests cross-context event aggregation, ordering, limiting, and caching.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class ActivityTimelineServiceTests : IDisposable
{
    private readonly MeepleAiDbContext _dbContext;
    private readonly Mock<IHybridCacheService> _mockCache;
    private readonly Mock<ILogger<ActivityTimelineService>> _mockLogger;
    private readonly ActivityTimelineService _service;
    private readonly Guid _userId = Guid.NewGuid();

    public ActivityTimelineServiceTests()
    {
        _dbContext = TestDbContextFactory.CreateInMemoryDbContext();
        _mockCache = new Mock<IHybridCacheService>();
        _mockLogger = new Mock<ILogger<ActivityTimelineService>>();

        // Configure cache to always execute factory (bypass cache for unit tests)
        _mockCache
            .Setup(c => c.GetOrCreateAsync(
                It.IsAny<string>(),
                It.IsAny<Func<CancellationToken, Task<IReadOnlyList<ActivityEvent>>>>(),
                It.IsAny<string[]?>(),
                It.IsAny<TimeSpan?>(),
                It.IsAny<CancellationToken>()))
            .Returns<string, Func<CancellationToken, Task<IReadOnlyList<ActivityEvent>>>, string[]?, TimeSpan?, CancellationToken>(
                (key, factory, tags, exp, ct) => factory(ct));

        _service = new ActivityTimelineService(_dbContext, _mockCache.Object, _mockLogger.Object);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task GetRecentActivities_ReturnsEventsInChronologicalOrder_Descending()
    {
        // Arrange - seed events with known timestamps
        var game = SeedSharedGame("Catan");
        var entry = SeedLibraryEntry(game, DateTime.UtcNow.AddDays(-3));
        SeedChatThread("Rules help", DateTime.UtcNow.AddDays(-1));
        SeedSessionOnEntry(entry, DateTime.UtcNow.AddDays(-2), 60);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _service.GetRecentActivitiesAsync(_userId);

        // Assert
        Assert.Equal(3, result.Count);
        Assert.True(result[0].Timestamp >= result[1].Timestamp);
        Assert.True(result[1].Timestamp >= result[2].Timestamp);
        Assert.Equal("chat_saved", result[0].Type);
        Assert.Equal("session_completed", result[1].Type);
        Assert.Equal("game_added", result[2].Type);
    }

    [Fact]
    public async Task GetRecentActivities_RespectsLimitParameter()
    {
        // Arrange - seed more events than limit
        for (int i = 0; i < 5; i++)
        {
            var game = SeedSharedGame($"Game {i}");
            SeedLibraryEntry(game, DateTime.UtcNow.AddDays(-i));
        }
        for (int i = 0; i < 5; i++)
        {
            SeedChatThread($"Chat {i}", DateTime.UtcNow.AddDays(-i));
        }
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _service.GetRecentActivitiesAsync(_userId, limit: 3);

        // Assert
        Assert.Equal(3, result.Count);
    }

    [Fact]
    public async Task GetRecentActivities_ReturnsEmptyList_WhenNoEvents()
    {
        // Act
        var result = await _service.GetRecentActivitiesAsync(_userId);

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public async Task GetRecentActivities_MergesEventsFromAllSources()
    {
        // Arrange
        var game = SeedSharedGame("Wingspan");
        var entry = SeedLibraryEntry(game, DateTime.UtcNow.AddHours(-1));
        SeedSessionOnEntry(entry, DateTime.UtcNow.AddHours(-2), 45);
        SeedChatThread("How to play", DateTime.UtcNow.AddHours(-3));
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _service.GetRecentActivitiesAsync(_userId);

        // Assert
        Assert.Equal(3, result.Count);
        var types = result.Select(e => e.Type).ToHashSet();
        Assert.Contains("game_added", types);
        Assert.Contains("session_completed", types);
        Assert.Contains("chat_saved", types);
    }

    [Fact]
    public async Task GetRecentActivities_ReturnsCorrectGameAddedEventFields()
    {
        // Arrange
        var game = SeedSharedGame("Azul", "https://example.com/azul.jpg");
        SeedLibraryEntry(game, DateTime.UtcNow);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _service.GetRecentActivitiesAsync(_userId);

        // Assert
        Assert.Single(result);
        var evt = Assert.IsType<GameAddedEvent>(result[0]);
        Assert.Equal("game_added", evt.Type);
        Assert.Equal("Azul", evt.GameName);
        Assert.Equal(game.Id, evt.GameId);
        Assert.Equal("https://example.com/azul.jpg", evt.CoverUrl);
    }

    [Fact]
    public async Task GetRecentActivities_ReturnsCorrectSessionCompletedEventFields()
    {
        // Arrange
        var game = SeedSharedGame("Root");
        var entry = SeedLibraryEntry(game, DateTime.UtcNow.AddDays(-10));
        SeedSessionOnEntry(entry, DateTime.UtcNow, 120);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _service.GetRecentActivitiesAsync(_userId);

        // Assert - library entry + session = 2 events
        Assert.Equal(2, result.Count);
        var sessionEvt = result.OfType<SessionCompletedEvent>().Single();
        Assert.Equal("session_completed", sessionEvt.Type);
        Assert.Equal("Root", sessionEvt.GameName);
        Assert.Equal(120, sessionEvt.Duration);
    }

    [Fact]
    public async Task GetRecentActivities_ReturnsCorrectChatSavedEventFields()
    {
        // Arrange
        SeedChatThread("Strategy discussion", DateTime.UtcNow);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _service.GetRecentActivitiesAsync(_userId);

        // Assert
        Assert.Single(result);
        var evt = Assert.IsType<ChatSavedEvent>(result[0]);
        Assert.Equal("chat_saved", evt.Type);
        Assert.Equal("Strategy discussion", evt.Topic);
    }

    [Fact]
    public async Task GetRecentActivities_ReturnsWishlistEvents()
    {
        // Arrange
        var game = SeedSharedGame("Gloomhaven");
        SeedLibraryEntry(game, DateTime.UtcNow, currentState: 3); // 3 = Wishlist
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _service.GetRecentActivitiesAsync(_userId);

        // Assert
        // Should have both game_added and wishlist_added for the same entry
        var wishlistEvents = result.Where(e => e.Type == "wishlist_added").ToList();
        Assert.Single(wishlistEvents);
        var evt = Assert.IsType<WishlistAddedEvent>(wishlistEvents[0]);
        Assert.Equal("Gloomhaven", evt.GameName);
    }

    [Fact]
    public async Task GetRecentActivities_OnlyReturnsEventsForRequestedUser()
    {
        // Arrange
        var game = SeedSharedGame("Pandemic");
        SeedLibraryEntry(game, DateTime.UtcNow);

        // Seed events for another user
        var otherUserId = Guid.NewGuid();
        var otherEntry = new UserLibraryEntryEntity
        {
            Id = Guid.NewGuid(),
            UserId = otherUserId,
            SharedGameId = game.Id,
            AddedAt = DateTime.UtcNow,
            SharedGame = game
        };
        _dbContext.UserLibraryEntries.Add(otherEntry);

        var otherChat = new ChatThreadEntity
        {
            Id = Guid.NewGuid(),
            UserId = otherUserId,
            Title = "Other user's chat",
            LastMessageAt = DateTime.UtcNow
        };
        _dbContext.ChatThreads.Add(otherChat);

        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _service.GetRecentActivitiesAsync(_userId);

        // Assert - should only have our user's events
        Assert.Single(result); // Only the game_added for our user
    }

    [Fact]
    public async Task GetRecentActivities_DefaultLimitIsTen()
    {
        // Arrange - seed 15 events
        for (int i = 0; i < 15; i++)
        {
            SeedChatThread($"Chat {i}", DateTime.UtcNow.AddMinutes(-i));
        }
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _service.GetRecentActivitiesAsync(_userId);

        // Assert
        Assert.Equal(10, result.Count);
    }

    [Fact]
    public async Task GetRecentActivities_UsesCacheWithCorrectKeyAndTags()
    {
        // Act
        await _service.GetRecentActivitiesAsync(_userId, limit: 5);

        // Assert
        _mockCache.Verify(c => c.GetOrCreateAsync(
            $"activity-timeline:{_userId}:5",
            It.IsAny<Func<CancellationToken, Task<IReadOnlyList<ActivityEvent>>>>(),
            It.Is<string[]?>(tags => tags != null && tags.Contains("activity-timeline") && tags.Contains($"user:{_userId}")),
            It.IsAny<TimeSpan?>(),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // --- Helper methods ---

    private SharedGameEntity SeedSharedGame(string title, string? thumbnailUrl = null)
    {
        var game = new SharedGameEntity
        {
            Id = Guid.NewGuid(),
            Title = title,
            ThumbnailUrl = thumbnailUrl ?? string.Empty,
            ImageUrl = string.Empty
        };
        _dbContext.SharedGames.Add(game);
        return game;
    }

    private UserLibraryEntryEntity SeedLibraryEntry(SharedGameEntity game, DateTime addedAt, int currentState = 0)
    {
        var entry = new UserLibraryEntryEntity
        {
            Id = Guid.NewGuid(),
            UserId = _userId,
            SharedGameId = game.Id,
            AddedAt = addedAt,
            CurrentState = currentState,
            StateChangedAt = currentState == 3 ? addedAt : null,
            SharedGame = game
        };
        _dbContext.UserLibraryEntries.Add(entry);
        return entry;
    }

    private void SeedSessionOnEntry(UserLibraryEntryEntity entry, DateTime playedAt, int durationMinutes)
    {
        var session = new UserGameSessionEntity
        {
            Id = Guid.NewGuid(),
            UserLibraryEntryId = entry.Id,
            PlayedAt = playedAt,
            DurationMinutes = durationMinutes,
            UserLibraryEntry = entry
        };
        entry.Sessions.Add(session);
    }

    private void SeedChatThread(string title, DateTime lastMessageAt)
    {
        var thread = new ChatThreadEntity
        {
            Id = Guid.NewGuid(),
            UserId = _userId,
            Title = title,
            LastMessageAt = lastMessageAt
        };
        _dbContext.ChatThreads.Add(thread);
    }
}
