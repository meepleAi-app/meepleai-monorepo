using Api.BoundedContexts.SharedGameCatalog.Application.DTOs;
using Api.BoundedContexts.SharedGameCatalog.Domain.Enums;
using Api.Infrastructure;
using Api.Infrastructure.Entities.SharedGameCatalog;
using Api.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Queries.GetCatalogTrending;

/// <summary>
/// Handler for GetCatalogTrendingQuery.
/// Returns cached trending games or computes from analytics events.
/// Issue #3918: Catalog Trending Analytics Service
/// </summary>
internal sealed class GetCatalogTrendingQueryHandler : IRequestHandler<GetCatalogTrendingQuery, List<TrendingGameDto>>
{
    private const string CacheKey = "catalog:trending";
    private const int MaxCacheLimit = 50;
    private static readonly TimeSpan CacheTtl = TimeSpan.FromHours(12);

    private readonly MeepleAiDbContext _context;
    private readonly IHybridCacheService _cache;
    private readonly ILogger<GetCatalogTrendingQueryHandler> _logger;

    public GetCatalogTrendingQueryHandler(
        MeepleAiDbContext context,
        IHybridCacheService cache,
        ILogger<GetCatalogTrendingQueryHandler> logger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<List<TrendingGameDto>> Handle(
        GetCatalogTrendingQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        // Always cache the full set (MaxCacheLimit), then trim to requested limit
        var allTrending = await _cache.GetOrCreateAsync(
            CacheKey,
            async ct => await ComputeTrendingAsync(MaxCacheLimit, ct).ConfigureAwait(false),
            tags: ["catalog", "trending"],
            expiration: CacheTtl,
            ct: cancellationToken).ConfigureAwait(false);

        // Trim to requested limit and re-rank
        var trending = allTrending.Take(query.Limit).ToList();

        _logger.LogInformation("Retrieved {Count} trending games from cache/compute", trending.Count);

        return trending;
    }

    private async Task<List<TrendingGameDto>> ComputeTrendingAsync(int limit, CancellationToken cancellationToken)
    {
        var sevenDaysAgo = DateTime.UtcNow.AddDays(-7);

        // Get all events from last 7 days
        var events = await _context.Set<GameAnalyticsEventEntity>()
            .AsNoTracking()
            .Where(e => e.Timestamp >= sevenDaysAgo)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (events.Count == 0)
        {
            _logger.LogInformation("No analytics events found in the last 7 days");
            return new List<TrendingGameDto>();
        }

        // Calculate weighted scores per game
        var gameScores = events
            .GroupBy(e => e.GameId)
            .Select(g =>
            {
                var gameEvents = g.ToList();
                var score = gameEvents.Sum(e =>
                {
                    var weight = GetEventWeight((GameEventType)e.EventType);
                    var daysAgo = (DateTime.UtcNow - e.Timestamp).TotalDays;
                    return weight * Math.Exp(-daysAgo / 7.0);
                });

                return new
                {
                    GameId = g.Key,
                    Score = score,
                    SearchCount = gameEvents.Count(e => e.EventType == (int)GameEventType.Search),
                    ViewCount = gameEvents.Count(e => e.EventType == (int)GameEventType.View),
                    LibraryAddCount = gameEvents.Count(e => e.EventType == (int)GameEventType.LibraryAdd),
                    PlayCount = gameEvents.Count(e => e.EventType == (int)GameEventType.Play)
                };
            })
            .OrderByDescending(g => g.Score)
            .Take(limit)
            .ToList();

        // Fetch game details for the trending games
        var gameIds = gameScores.Select(g => g.GameId).ToList();
        var games = await _context.Set<SharedGameEntity>()
            .AsNoTracking()
            .Where(g => gameIds.Contains(g.Id))
            .Select(g => new { g.Id, g.Title, g.ThumbnailUrl })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var gameMap = games.ToDictionary(g => g.Id);

        // Build result DTOs with ranking
        var rank = 1;
        var result = gameScores.Select(g =>
        {
            gameMap.TryGetValue(g.GameId, out var game);
            return new TrendingGameDto
            {
                Rank = rank++,
                GameId = g.GameId,
                Title = game?.Title ?? "Unknown Game",
                ThumbnailUrl = game?.ThumbnailUrl,
                Score = Math.Round(g.Score, 2),
                SearchCount = g.SearchCount,
                ViewCount = g.ViewCount,
                LibraryAddCount = g.LibraryAddCount,
                PlayCount = g.PlayCount
            };
        }).ToList();

        _logger.LogInformation(
            "Computed trending scores for {GameCount} games from {EventCount} events",
            result.Count,
            events.Count);

        return result;
    }

    private static int GetEventWeight(GameEventType eventType)
    {
        return eventType switch
        {
            GameEventType.Search => 3,
            GameEventType.View => 1,
            GameEventType.LibraryAdd => 5,
            GameEventType.Play => 10,
            _ => 0
        };
    }
}
