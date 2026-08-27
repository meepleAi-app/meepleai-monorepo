using Api.BoundedContexts.Administration.Application.DTOs;
using Api.BoundedContexts.Administration.Application.Queries;
using Api.BoundedContexts.Administration.Domain.Services;
using Api.Infrastructure;
using Api.Services;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.Administration.Application.Services;

/// <summary>
/// Aggregates activity events from multiple bounded contexts (Issue #3973, #3923).
/// Fetches from UserLibrary, Sessions, Chat, and Wishlist sources in parallel,
/// merges chronologically, and caches per-user for 5 minutes.
/// Supports type filtering, text search, pagination, and sort order.
/// Performance target: &lt; 500ms with 1000 events.
/// </summary>
internal sealed class ActivityTimelineService : IActivityTimelineService
{
    private readonly MeepleAiDbContext _dbContext;
    private readonly IHybridCacheService _cache;
    private readonly ILogger<ActivityTimelineService> _logger;

    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);
    private const int MaxCacheLimit = 500;

    public ActivityTimelineService(
        MeepleAiDbContext dbContext,
        IHybridCacheService cache,
        ILogger<ActivityTimelineService> logger)
    {
        _dbContext = dbContext;
        _cache = cache;
        _logger = logger;
    }

    public async Task<IReadOnlyList<ActivityEvent>> GetRecentActivitiesAsync(
        Guid userId,
        int limit = 10,
        CancellationToken cancellationToken = default)
    {
        var cacheKey = $"activity-timeline:{userId}:{limit}";

        return await _cache.GetOrCreateAsync(
            cacheKey,
            async ct => await FetchAndMergeActivitiesAsync(userId, limit, ct).ConfigureAwait(false),
            tags: [$"activity-timeline", $"user:{userId}"],
            expiration: CacheDuration,
            ct: cancellationToken).ConfigureAwait(false);
    }

    public async Task<(IReadOnlyList<ActivityEvent> Events, int TotalCount)> GetFilteredActivitiesAsync(
        Guid userId,
        string[]? types = null,
        string? searchTerm = null,
        DateTime? dateFrom = null,
        DateTime? dateTo = null,
        int skip = 0,
        int take = 20,
        SortDirection order = SortDirection.Descending,
        CancellationToken cancellationToken = default)
    {
        // Cache the full event set per user, then filter/paginate in memory.
        var cacheKey = $"activity-timeline-full:{userId}";

        var allEvents = await _cache.GetOrCreateAsync(
            cacheKey,
            async ct => await FetchAllActivitiesAsync(userId, ct).ConfigureAwait(false),
            tags: [$"activity-timeline", $"user:{userId}"],
            expiration: CacheDuration,
            ct: cancellationToken).ConfigureAwait(false);

        // Apply type filter
        IEnumerable<ActivityEvent> filtered = allEvents;
        if (types is { Length: > 0 })
        {
            var typeSet = new HashSet<string>(types, StringComparer.OrdinalIgnoreCase);
            filtered = filtered.Where(e => typeSet.Contains(e.Type));
        }

        // Apply date range filter
        if (dateFrom.HasValue)
        {
            filtered = filtered.Where(e => e.Timestamp >= dateFrom.Value);
        }
        if (dateTo.HasValue)
        {
            filtered = filtered.Where(e => e.Timestamp <= dateTo.Value);
        }

        // Apply text search on GameName/Topic
        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var term = searchTerm.Trim();
            filtered = filtered.Where(e => MatchesSearch(e, term));
        }

        // Sort
        var sorted = order == SortDirection.Ascending
            ? filtered.OrderBy(e => e.Timestamp)
            : filtered.OrderByDescending(e => e.Timestamp);

        // Get total count before pagination
        var materialised = sorted.ToList();
        var totalCount = materialised.Count;

        // Apply pagination
        var paginated = materialised
            .Skip(skip)
            .Take(take)
            .ToList();

        return (paginated, totalCount);
    }

    private static bool MatchesSearch(ActivityEvent evt, string term)
    {
        return evt switch
        {
            GameAddedEvent e => e.GameName.Contains(term, StringComparison.OrdinalIgnoreCase),
            SessionCompletedEvent e => e.GameName.Contains(term, StringComparison.OrdinalIgnoreCase),
            ChatSavedEvent e => e.Topic.Contains(term, StringComparison.OrdinalIgnoreCase),
            WishlistAddedEvent e => e.GameName.Contains(term, StringComparison.OrdinalIgnoreCase),
            _ => false
        };
    }

    private async Task<IReadOnlyList<ActivityEvent>> FetchAllActivitiesAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        // Sequential on purpose (#3843): the four sources share the request-scoped
        // DbContext, which EF Core forbids using concurrently.
        var libraryResult = await GetLibraryEventsAsync(userId, MaxCacheLimit, cancellationToken).ConfigureAwait(false);
        var sessionResult = await GetSessionEventsAsync(userId, MaxCacheLimit, cancellationToken).ConfigureAwait(false);
        var chatResult = await GetChatEventsAsync(userId, MaxCacheLimit, cancellationToken).ConfigureAwait(false);
        var wishlistResult = await GetWishlistEventsAsync(userId, MaxCacheLimit, cancellationToken).ConfigureAwait(false);

        return libraryResult
            .Concat(sessionResult)
            .Concat(chatResult)
            .Concat(wishlistResult)
            .OrderByDescending(e => e.Timestamp)
            .ToList();
    }

    private async Task<IReadOnlyList<ActivityEvent>> FetchAndMergeActivitiesAsync(
        Guid userId,
        int limit,
        CancellationToken cancellationToken)
    {
        // Each source fetches 'limit' items, then we merge and take top 'limit' overall.
        // Sequential on purpose (#3843): the four sources share the request-scoped
        // DbContext, which EF Core forbids using concurrently.
        var libResult = await GetLibraryEventsAsync(userId, limit, cancellationToken).ConfigureAwait(false);
        var sessResult = await GetSessionEventsAsync(userId, limit, cancellationToken).ConfigureAwait(false);
        var chatResult2 = await GetChatEventsAsync(userId, limit, cancellationToken).ConfigureAwait(false);
        var wishResult = await GetWishlistEventsAsync(userId, limit, cancellationToken).ConfigureAwait(false);

        var allEvents = libResult
            .Concat(sessResult)
            .Concat(chatResult2)
            .Concat(wishResult)
            .OrderByDescending(e => e.Timestamp)
            .Take(limit)
            .ToList();

        return allEvents;
    }

    private async Task<IReadOnlyList<ActivityEvent>> GetLibraryEventsAsync(
        Guid userId, int limit, CancellationToken cancellationToken)
    {
        try
        {
            return await _dbContext.UserLibraryEntries
                .AsNoTracking()
                .Include(e => e.SharedGame)
                .Where(e => e.UserId == userId && e.SharedGame != null)
                .OrderByDescending(e => e.AddedAt)
                .Take(limit)
                .Select(e => (ActivityEvent)new GameAddedEvent
                {
                    Id = e.Id,
                    Timestamp = e.AddedAt,
                    Type = "game_added",
                    GameId = e.SharedGameId ?? Guid.Empty,
                    GameName = e.SharedGame!.Title,
                    CoverUrl = e.SharedGame.ThumbnailUrl
                })
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch library events for user {UserId}", userId);
            return [];
        }
    }

    private async Task<IReadOnlyList<ActivityEvent>> GetSessionEventsAsync(
        Guid userId, int limit, CancellationToken cancellationToken)
    {
        try
        {
            var sessions = await _dbContext.UserLibraryEntries
                .AsNoTracking()
                .Include(e => e.Sessions)
                .Include(e => e.SharedGame)
                .Where(e => e.UserId == userId && e.SharedGame != null)
                .SelectMany(e => e.Sessions.Select(s => new
                {
                    s.Id,
                    s.PlayedAt,
                    s.DurationMinutes,
                    GameTitle = e.SharedGame!.Title
                }))
                .OrderByDescending(s => s.PlayedAt)
                .Take(limit)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            return sessions.Select(s => (ActivityEvent)new SessionCompletedEvent
            {
                Id = s.Id,
                Timestamp = s.PlayedAt,
                Type = "session_completed",
                SessionId = s.Id,
                GameName = s.GameTitle,
                Duration = s.DurationMinutes
            }).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch session events for user {UserId}", userId);
            return [];
        }
    }

    private async Task<IReadOnlyList<ActivityEvent>> GetChatEventsAsync(
        Guid userId, int limit, CancellationToken cancellationToken)
    {
        try
        {
            return await _dbContext.ChatThreads
                .AsNoTracking()
                .Where(c => c.UserId == userId)
                .OrderByDescending(c => c.LastMessageAt)
                .Take(limit)
                .Select(c => (ActivityEvent)new ChatSavedEvent
                {
                    Id = c.Id,
                    Timestamp = c.LastMessageAt,
                    Type = "chat_saved",
                    ChatId = c.Id,
                    Topic = c.Title ?? "Chat"
                })
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch chat events for user {UserId}", userId);
            return [];
        }
    }

    private async Task<IReadOnlyList<ActivityEvent>> GetWishlistEventsAsync(
        Guid userId, int limit, CancellationToken cancellationToken)
    {
        try
        {
            // Wishlist entries are library entries with CurrentState = 3 (Wishlist enum value)
            return await _dbContext.UserLibraryEntries
                .AsNoTracking()
                .Include(e => e.SharedGame)
                .Where(e => e.UserId == userId && e.CurrentState == 3 && e.SharedGame != null)
                .OrderByDescending(e => e.StateChangedAt ?? e.AddedAt)
                .Take(limit)
                .Select(e => (ActivityEvent)new WishlistAddedEvent
                {
                    Id = e.Id,
                    Timestamp = e.StateChangedAt ?? e.AddedAt,
                    Type = "wishlist_added",
                    GameId = e.SharedGameId ?? Guid.Empty,
                    GameName = e.SharedGame!.Title
                })
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch wishlist events for user {UserId}", userId);
            return [];
        }
    }
}
