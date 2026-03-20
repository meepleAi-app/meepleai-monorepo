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
using FluentAssertions;
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
        totalCount.Should().Be(1);
        events.Should().ContainSingle();
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
        totalCount.Should().Be(2);
        events.Should().Contain(e => e.Type == "game_added");
        events.Should().Contain(e => e.Type == "session_completed");
        events.Should().NotContain(e => e.Type == "chat_saved");
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
        events.Should().ContainSingle();
        events[0].Type.Should().Be("game_added");
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
        totalCount.Should().Be(3);
        var types = events.Select(e => e.Type).ToHashSet();
        types.Should().Contain("game_added");
        types.Should().Contain("session_completed");
        types.Should().Contain("chat_saved");
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
        totalCount.Should().Be(2);
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
        events.Should().ContainSingle();
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
        totalCount.Should().Be(1);
        events[0].Should().BeOfType<ChatSavedEvent>();
        var chatEvt = (ChatSavedEvent)events[0];
        chatEvt.Topic.Should().ContainEquivalentOf("Wingspan");
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
        totalCount.Should().Be(2);
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
        totalCount.Should().Be(0);
        events.Should().BeEmpty();
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
        total1.Should().Be(10);
        total2.Should().Be(10);
        page1.Count.Should().Be(3);
        page2.Count.Should().Be(3);

        // Pages should not overlap
        var page1Ids = page1.Select(e => e.Id).ToHashSet();
        var page2Ids = page2.Select(e => e.Id).ToHashSet();
        page1Ids.Intersect(page2Ids).Should().BeEmpty();
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
        totalCount.Should().Be(1);
        events.Should().BeEmpty();
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
        totalCount.Should().Be(25);
        events.Count.Should().Be(20);
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
        (events[0].Timestamp >= events[1].Timestamp).Should().BeTrue();
        (events[1].Timestamp >= events[2].Timestamp).Should().BeTrue();
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
        (events[0].Timestamp <= events[1].Timestamp).Should().BeTrue();
        (events[1].Timestamp <= events[2].Timestamp).Should().BeTrue();
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
        totalCount.Should().Be(1);
        events.Should().ContainSingle();
        events[0].Type.Should().Be("game_added");
        events[0].Should().BeOfType<GameAddedEvent>();
        var gameEvt = (GameAddedEvent)events[0];
        gameEvt.GameName.Should().Be("Wingspan");
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
        totalCount.Should().Be(5);
        events.Count.Should().Be(2);
        (events[0].Timestamp <= events[1].Timestamp).Should().BeTrue();
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
        totalCount.Should().Be(1);
        events.Should().ContainSingle();
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
        totalCount.Should().Be(1);
        events.Should().ContainSingle();
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
        totalCount.Should().Be(1);
        events.Should().ContainSingle();
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
        totalCount.Should().Be(1);
        events.Should().ContainSingle();
        events[0].Type.Should().Be("game_added");
    }

    #endregion

    #region Edge Cases

    [Fact]
    public async Task GetFilteredActivities_ReturnsEmpty_WhenNoEvents()
    {
        // Act
        var (events, totalCount) = await _service.GetFilteredActivitiesAsync(_userId);

        // Assert
        totalCount.Should().Be(0);
        events.Should().BeEmpty();
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
        totalCount.Should().Be(1);
        events.Should().ContainSingle();
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
        totalCount.Should().Be(2);
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
        totalCount.Should().Be(1);
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
        totalCount.Should().Be(1);
        events.Should().ContainSingle();
        events[0].Type.Should().Be("wishlist_added");
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
