using Api.BoundedContexts.Administration.Application.DTOs;
using Api.BoundedContexts.Administration.Application.Queries;
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
/// Unit tests for ActivityTimelineService.GetFilteredActivitiesAsync (Issue #3923).
/// Tests type filtering, text search, pagination, sorting, and combined filters.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public sealed class ActivityTimelineServiceFilterTests : IDisposable
{
    private readonly MeepleAiDbContext _dbContext;
    private readonly Mock<IHybridCacheService> _mockCache;
    private readonly Mock<ILogger<ActivityTimelineService>> _mockLogger;
    private readonly ActivityTimelineService _service;
    private readonly Guid _userId = Guid.NewGuid();

    public ActivityTimelineServiceFilterTests()
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

    #region Type Filtering

    [Fact]
    public async Task GetFilteredActivities_FiltersBySingleType()
    {
        // Arrange
        var game = SeedSharedGame("Catan");
        SeedLibraryEntry(game, DateTime.UtcNow.AddDays(-1));
        SeedChatThread("Rules help", DateTime.UtcNow.AddDays(-2));
        await _dbContext.SaveChangesAsync();

        // Act
        var (events, totalCount) = await _service.GetFilteredActivitiesAsync(
            _userId,
            types: ["game_added"]);

        // Assert
        Assert.Equal(1, totalCount);
        Assert.Single(events);
        Assert.All(events, e => Assert.Equal("game_added", e.Type));
    }

    [Fact]
    public async Task GetFilteredActivities_FiltersByMultipleTypes()
    {
        // Arrange
        var game = SeedSharedGame("Wingspan");
        var entry = SeedLibraryEntry(game, DateTime.UtcNow.AddDays(-1));
        SeedSessionOnEntry(entry, DateTime.UtcNow.AddDays(-2), 90);
        SeedChatThread("Strategy tips", DateTime.UtcNow.AddDays(-3));
        await _dbContext.SaveChangesAsync();

        // Act
        var (events, totalCount) = await _service.GetFilteredActivitiesAsync(
            _userId,
            types: ["game_added", "session_completed"]);

        // Assert
        Assert.Equal(2, totalCount);
        Assert.Contains(events, e => e.Type == "game_added");
        Assert.Contains(events, e => e.Type == "session_completed");
        Assert.DoesNotContain(events, e => e.Type == "chat_saved");
    }

    [Fact]
    public async Task GetFilteredActivities_TypeFilterIsCaseInsensitive()
    {
        // Arrange
        var game = SeedSharedGame("Azul");
        SeedLibraryEntry(game, DateTime.UtcNow);
        await _dbContext.SaveChangesAsync();

        // Act
        var (events, _) = await _service.GetFilteredActivitiesAsync(
            _userId,
            types: ["GAME_ADDED"]);

        // Assert
        Assert.Single(events);
        Assert.Equal("game_added", events[0].Type);
    }

    [Fact]
    public async Task GetFilteredActivities_ReturnsAllTypes_WhenNoTypeFilter()
    {
        // Arrange
        var game = SeedSharedGame("Pandemic");
        var entry = SeedLibraryEntry(game, DateTime.UtcNow.AddDays(-1));
        SeedSessionOnEntry(entry, DateTime.UtcNow.AddDays(-2), 60);
        SeedChatThread("How to win", DateTime.UtcNow.AddDays(-3));
        await _dbContext.SaveChangesAsync();

        // Act
        var (events, totalCount) = await _service.GetFilteredActivitiesAsync(
            _userId);

        // Assert
        Assert.Equal(3, totalCount);
        var types = events.Select(e => e.Type).ToHashSet();
        Assert.Contains("game_added", types);
        Assert.Contains("session_completed", types);
        Assert.Contains("chat_saved", types);
    }

    #endregion

    #region Text Search

    [Fact]
    public async Task GetFilteredActivities_SearchFindsPartialMatchOnGameName()
    {
        // Arrange
        SeedSharedGameAndEntry("Wingspan", DateTime.UtcNow.AddDays(-1));
        SeedSharedGameAndEntry("Catan", DateTime.UtcNow.AddDays(-2));
        SeedSharedGameAndEntry("Wing It", DateTime.UtcNow.AddDays(-3));
        await _dbContext.SaveChangesAsync();

        // Act
        var (events, totalCount) = await _service.GetFilteredActivitiesAsync(
            _userId,
            searchTerm: "wing");

        // Assert
        Assert.Equal(2, totalCount);
        Assert.All(events, e =>
        {
            var gameEvt = Assert.IsType<GameAddedEvent>(e);
            Assert.Contains("Wing", gameEvt.GameName, StringComparison.OrdinalIgnoreCase);
        });
    }

    [Fact]
    public async Task GetFilteredActivities_SearchIsCaseInsensitive()
    {
        // Arrange
        SeedSharedGameAndEntry("Terraforming Mars", DateTime.UtcNow);
        await _dbContext.SaveChangesAsync();

        // Act
        var (events, _) = await _service.GetFilteredActivitiesAsync(
            _userId,
            searchTerm: "TERRAFORMING");

        // Assert
        Assert.Single(events);
    }

    [Fact]
    public async Task GetFilteredActivities_SearchMatchesChatTopic()
    {
        // Arrange
        SeedChatThread("How to play Wingspan", DateTime.UtcNow.AddDays(-1));
        SeedChatThread("Strategy guide", DateTime.UtcNow.AddDays(-2));
        await _dbContext.SaveChangesAsync();

        // Act
        var (events, totalCount) = await _service.GetFilteredActivitiesAsync(
            _userId,
            searchTerm: "wingspan");

        // Assert
        Assert.Equal(1, totalCount);
        var chatEvt = Assert.IsType<ChatSavedEvent>(events[0]);
        Assert.Contains("Wingspan", chatEvt.Topic, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetFilteredActivities_SearchMatchesSessionGameName()
    {
        // Arrange
        var game = SeedSharedGame("Gloomhaven");
        var entry = SeedLibraryEntry(game, DateTime.UtcNow.AddDays(-5));
        SeedSessionOnEntry(entry, DateTime.UtcNow, 120);
        SeedChatThread("Unrelated chat", DateTime.UtcNow.AddDays(-1));
        await _dbContext.SaveChangesAsync();

        // Act
        var (events, totalCount) = await _service.GetFilteredActivitiesAsync(
            _userId,
            searchTerm: "gloom");

        // Assert
        Assert.Equal(2, totalCount); // game_added + session_completed both match
        Assert.All(events, e =>
        {
            Assert.True(e.Type == "game_added" || e.Type == "session_completed");
        });
    }

    [Fact]
    public async Task GetFilteredActivities_SearchReturnsEmpty_WhenNoMatch()
    {
        // Arrange
        SeedSharedGameAndEntry("Catan", DateTime.UtcNow);
        await _dbContext.SaveChangesAsync();

        // Act
        var (events, totalCount) = await _service.GetFilteredActivitiesAsync(
            _userId,
            searchTerm: "nonexistent");

        // Assert
        Assert.Equal(0, totalCount);
        Assert.Empty(events);
    }

    #endregion

    #region Pagination

    [Fact]
    public async Task GetFilteredActivities_PaginationWorksCorrectly()
    {
        // Arrange - seed 10 events
        for (int i = 0; i < 10; i++)
        {
            SeedChatThread($"Chat {i}", DateTime.UtcNow.AddMinutes(-i));
        }
        await _dbContext.SaveChangesAsync();

        // Act - page 1 (first 3)
        var (page1, total1) = await _service.GetFilteredActivitiesAsync(
            _userId, skip: 0, take: 3);

        // Act - page 2 (next 3)
        var (page2, total2) = await _service.GetFilteredActivitiesAsync(
            _userId, skip: 3, take: 3);

        // Assert
        Assert.Equal(10, total1);
        Assert.Equal(10, total2);
        Assert.Equal(3, page1.Count);
        Assert.Equal(3, page2.Count);

        // Pages should not overlap
        var page1Ids = page1.Select(e => e.Id).ToHashSet();
        var page2Ids = page2.Select(e => e.Id).ToHashSet();
        Assert.Empty(page1Ids.Intersect(page2Ids));
    }

    [Fact]
    public async Task GetFilteredActivities_SkipBeyondTotal_ReturnsEmpty()
    {
        // Arrange
        SeedSharedGameAndEntry("Catan", DateTime.UtcNow);
        await _dbContext.SaveChangesAsync();

        // Act
        var (events, totalCount) = await _service.GetFilteredActivitiesAsync(
            _userId, skip: 100, take: 20);

        // Assert
        Assert.Equal(1, totalCount);
        Assert.Empty(events);
    }

    [Fact]
    public async Task GetFilteredActivities_DefaultPaginationReturns20()
    {
        // Arrange - seed 25 events
        for (int i = 0; i < 25; i++)
        {
            SeedChatThread($"Chat {i}", DateTime.UtcNow.AddMinutes(-i));
        }
        await _dbContext.SaveChangesAsync();

        // Act - default skip=0, take=20
        var (events, totalCount) = await _service.GetFilteredActivitiesAsync(_userId);

        // Assert
        Assert.Equal(25, totalCount);
        Assert.Equal(20, events.Count);
    }

    #endregion

    #region Sorting

    [Fact]
    public async Task GetFilteredActivities_SortsDescendingByDefault()
    {
        // Arrange
        SeedChatThread("Old chat", DateTime.UtcNow.AddDays(-3));
        SeedChatThread("New chat", DateTime.UtcNow.AddDays(-1));
        SeedChatThread("Mid chat", DateTime.UtcNow.AddDays(-2));
        await _dbContext.SaveChangesAsync();

        // Act
        var (events, _) = await _service.GetFilteredActivitiesAsync(_userId);

        // Assert
        Assert.True(events[0].Timestamp >= events[1].Timestamp);
        Assert.True(events[1].Timestamp >= events[2].Timestamp);
    }

    [Fact]
    public async Task GetFilteredActivities_SortsAscendingWhenRequested()
    {
        // Arrange
        SeedChatThread("Old chat", DateTime.UtcNow.AddDays(-3));
        SeedChatThread("New chat", DateTime.UtcNow.AddDays(-1));
        SeedChatThread("Mid chat", DateTime.UtcNow.AddDays(-2));
        await _dbContext.SaveChangesAsync();

        // Act
        var (events, _) = await _service.GetFilteredActivitiesAsync(
            _userId,
            order: SortDirection.Ascending);

        // Assert
        Assert.True(events[0].Timestamp <= events[1].Timestamp);
        Assert.True(events[1].Timestamp <= events[2].Timestamp);
    }

    #endregion

    #region Combined Filters

    [Fact]
    public async Task GetFilteredActivities_CombinesTypeFilterAndSearch()
    {
        // Arrange
        SeedSharedGameAndEntry("Wingspan", DateTime.UtcNow.AddDays(-1));
        SeedSharedGameAndEntry("Catan", DateTime.UtcNow.AddDays(-2));
        SeedChatThread("Wingspan rules", DateTime.UtcNow.AddDays(-3));
        await _dbContext.SaveChangesAsync();

        // Act - filter to game_added + search for "wingspan"
        var (events, totalCount) = await _service.GetFilteredActivitiesAsync(
            _userId,
            types: ["game_added"],
            searchTerm: "wingspan");

        // Assert - only game_added events matching "wingspan"
        Assert.Equal(1, totalCount);
        Assert.Single(events);
        Assert.Equal("game_added", events[0].Type);
        var gameEvt = Assert.IsType<GameAddedEvent>(events[0]);
        Assert.Equal("Wingspan", gameEvt.GameName);
    }

    [Fact]
    public async Task GetFilteredActivities_CombinesAllFilters()
    {
        // Arrange - seed various events
        for (int i = 0; i < 5; i++)
        {
            SeedSharedGameAndEntry($"Wingspan Edition {i}", DateTime.UtcNow.AddDays(-i));
        }
        SeedSharedGameAndEntry("Catan", DateTime.UtcNow.AddDays(-6));
        SeedChatThread("Wingspan strategy", DateTime.UtcNow.AddDays(-7));
        await _dbContext.SaveChangesAsync();

        // Act - type=game_added, search=wingspan, skip=1, take=2, order=asc
        var (events, totalCount) = await _service.GetFilteredActivitiesAsync(
            _userId,
            types: ["game_added"],
            searchTerm: "wingspan",
            skip: 1,
            take: 2,
            order: SortDirection.Ascending);

        // Assert
        Assert.Equal(5, totalCount); // 5 Wingspan game_added events
        Assert.Equal(2, events.Count); // take=2
        Assert.True(events[0].Timestamp <= events[1].Timestamp); // ascending
    }

    #endregion

    #region Date Range Filtering

    [Fact]
    public async Task GetFilteredActivities_FiltersByDateFrom()
    {
        // Arrange
        SeedChatThread("Old chat", new DateTime(2025, 6, 1, 0, 0, 0, DateTimeKind.Utc));
        SeedChatThread("New chat", new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc));
        await _dbContext.SaveChangesAsync();

        // Act
        var (events, totalCount) = await _service.GetFilteredActivitiesAsync(
            _userId,
            dateFrom: new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));

        // Assert
        Assert.Equal(1, totalCount);
        Assert.Single(events);
    }

    [Fact]
    public async Task GetFilteredActivities_FiltersByDateTo()
    {
        // Arrange
        SeedChatThread("Old chat", new DateTime(2025, 6, 1, 0, 0, 0, DateTimeKind.Utc));
        SeedChatThread("New chat", new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc));
        await _dbContext.SaveChangesAsync();

        // Act
        var (events, totalCount) = await _service.GetFilteredActivitiesAsync(
            _userId,
            dateTo: new DateTime(2025, 12, 31, 0, 0, 0, DateTimeKind.Utc));

        // Assert
        Assert.Equal(1, totalCount);
        Assert.Single(events);
    }

    [Fact]
    public async Task GetFilteredActivities_FiltersByDateRange()
    {
        // Arrange
        SeedChatThread("Before", new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        SeedChatThread("In range", new DateTime(2025, 7, 15, 0, 0, 0, DateTimeKind.Utc));
        SeedChatThread("After", new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc));
        await _dbContext.SaveChangesAsync();

        // Act
        var (events, totalCount) = await _service.GetFilteredActivitiesAsync(
            _userId,
            dateFrom: new DateTime(2025, 6, 1, 0, 0, 0, DateTimeKind.Utc),
            dateTo: new DateTime(2025, 12, 31, 0, 0, 0, DateTimeKind.Utc));

        // Assert
        Assert.Equal(1, totalCount);
        Assert.Single(events);
    }

    [Fact]
    public async Task GetFilteredActivities_DateRangeWithTypeFilter()
    {
        // Arrange
        SeedSharedGameAndEntry("Catan", new DateTime(2026, 1, 15, 0, 0, 0, DateTimeKind.Utc));
        SeedSharedGameAndEntry("Old Game", new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        SeedChatThread("Chat in range", new DateTime(2026, 1, 20, 0, 0, 0, DateTimeKind.Utc));
        await _dbContext.SaveChangesAsync();

        // Act
        var (events, totalCount) = await _service.GetFilteredActivitiesAsync(
            _userId,
            types: ["game_added"],
            dateFrom: new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            dateTo: new DateTime(2026, 12, 31, 0, 0, 0, DateTimeKind.Utc));

        // Assert
        Assert.Equal(1, totalCount);
        Assert.Single(events);
        Assert.Equal("game_added", events[0].Type);
    }

    #endregion

    #region Edge Cases

    [Fact]
    public async Task GetFilteredActivities_ReturnsEmpty_WhenNoEvents()
    {
        // Act
        var (events, totalCount) = await _service.GetFilteredActivitiesAsync(_userId);

        // Assert
        Assert.Equal(0, totalCount);
        Assert.Empty(events);
    }

    [Fact]
    public async Task GetFilteredActivities_NullTypesAndSearchAreIgnored()
    {
        // Arrange
        SeedSharedGameAndEntry("Catan", DateTime.UtcNow);
        await _dbContext.SaveChangesAsync();

        // Act
        var (events, totalCount) = await _service.GetFilteredActivitiesAsync(
            _userId,
            types: null,
            searchTerm: null);

        // Assert
        Assert.Equal(1, totalCount);
        Assert.Single(events);
    }

    [Fact]
    public async Task GetFilteredActivities_EmptyTypesArrayIsIgnored()
    {
        // Arrange
        SeedSharedGameAndEntry("Catan", DateTime.UtcNow);
        SeedChatThread("Strategy", DateTime.UtcNow.AddHours(-1));
        await _dbContext.SaveChangesAsync();

        // Act
        var (events, totalCount) = await _service.GetFilteredActivitiesAsync(
            _userId,
            types: []);

        // Assert
        Assert.Equal(2, totalCount);
    }

    [Fact]
    public async Task GetFilteredActivities_WhitespaceSearchIsIgnored()
    {
        // Arrange
        SeedSharedGameAndEntry("Catan", DateTime.UtcNow);
        await _dbContext.SaveChangesAsync();

        // Act
        var (events, totalCount) = await _service.GetFilteredActivitiesAsync(
            _userId,
            searchTerm: "   ");

        // Assert
        Assert.Equal(1, totalCount);
    }

    [Fact]
    public async Task GetFilteredActivities_FiltersWishlistEvents()
    {
        // Arrange
        var game = SeedSharedGame("Gloomhaven");
        SeedLibraryEntry(game, DateTime.UtcNow, currentState: 3); // Wishlist
        SeedChatThread("Random chat", DateTime.UtcNow.AddHours(-1));
        await _dbContext.SaveChangesAsync();

        // Act
        var (events, totalCount) = await _service.GetFilteredActivitiesAsync(
            _userId,
            types: ["wishlist_added"]);

        // Assert
        Assert.Equal(1, totalCount);
        Assert.Single(events);
        Assert.Equal("wishlist_added", events[0].Type);
    }

    #endregion

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

    private void SeedSharedGameAndEntry(string title, DateTime addedAt)
    {
        var game = SeedSharedGame(title);
        SeedLibraryEntry(game, addedAt);
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
