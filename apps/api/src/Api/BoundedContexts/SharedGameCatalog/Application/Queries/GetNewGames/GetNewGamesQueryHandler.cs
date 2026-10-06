using Api.BoundedContexts.SharedGameCatalog.Application.Services;
using Api.Infrastructure;
using Api.Infrastructure.Entities.SharedGameCatalog;
using Api.Services;
using Api.Services.Pdf;
using Api.SharedKernel.Domain.Covers;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Queries.GetNewGames;

/// <summary>
/// Handler for <see cref="GetNewGamesQuery"/> — returns the most recently
/// created shared games sorted by <c>CreatedAt DESC</c>.
/// Wave 3 Phase 1, PR #732 §4.3.2 / Issue #805.
/// </summary>
/// <remarks>
/// Strategy mirrors <see cref="GetCatalogTrending.GetCatalogTrendingQueryHandler"/>:
/// always materialize the full <see cref="MaxCacheLimit"/> set and trim to the
/// requested limit so a single cache entry serves all caller variants.
/// Cache TTL: 1 hour (PR #732 §3.2 caching matrix). Excludes soft-deleted rows.
/// </remarks>
internal sealed class GetNewGamesQueryHandler
    : IRequestHandler<GetNewGamesQuery, IReadOnlyList<NewGameDto>>
{
    private const string CacheKey = "discover:newGames";
    private const int MaxCacheLimit = 50;
    private static readonly TimeSpan CacheTtl = TimeSpan.FromHours(1);

    private readonly MeepleAiDbContext _context;
    private readonly IHybridCacheService _cache;
    private readonly IBlobStorageService _blobStorage;
    private readonly ILogger<GetNewGamesQueryHandler> _logger;

    public GetNewGamesQueryHandler(
        MeepleAiDbContext context,
        IHybridCacheService cache,
        IBlobStorageService blobStorage,
        ILogger<GetNewGamesQueryHandler> logger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _blobStorage = blobStorage ?? throw new ArgumentNullException(nameof(blobStorage));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<IReadOnlyList<NewGameDto>> Handle(
        GetNewGamesQuery request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var allNew = await _cache.GetOrCreateAsync(
            CacheKey,
            async ct => await ComputeNewGamesAsync(MaxCacheLimit, ct).ConfigureAwait(false),
            tags: ["catalog", "discover", "newGames"],
            expiration: CacheTtl,
            ct: cancellationToken).ConfigureAwait(false);

        var trimmed = allNew.Take(request.Limit).ToList();

        // #4085: resolve CoverUrl HERE, after the cache boundary, same reasoning as
        // GetCatalogTrendingQueryHandler (see TrendingGameDto.CoverUrl) — the resolver's
        // presigned URLs expire after 4h (CoverUrlResolver.CoverPresignExpirySeconds).
        // This handler's cache TTL is 1h (CacheTtl above), under that lifetime, so baking
        // the URL into the cached DTO here would in fact be safe today — but resolving it
        // post-cache instead means the TTL can be raised later without silently reopening
        // the same stale-presign bug, and keeps one shape for both catalog-row handlers.
        var gameIds = trimmed.Select(g => g.Id).ToList();
        var entities = await _context.Set<SharedGameEntity>()
            .AsNoTracking()
            .Where(g => gameIds.Contains(g.Id))
            .Include(g => g.CoverAssignments)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var entityMap = entities.ToDictionary(e => e.Id);

        var withCovers = new List<NewGameDto>(trimmed.Count);
        foreach (var g in trimmed)
        {
            string? coverUrl = null;
            if (entityMap.TryGetValue(g.Id, out var entity))
            {
                coverUrl = await CoverUrlResolver
                    .ResolveForContextAsync(entity, CoverContext.Card, _blobStorage)
                    .ConfigureAwait(false);
            }
            // #4085: overwrites ImageUrl directly (no separate CoverUrl field here, unlike
            // TrendingGameDto) — this DTO has exactly one image field and one FE consumer,
            // and g.ImageUrl itself is already sourced from the #2123 tombstone columns
            // (ImageUrl/ThumbnailUrl), so falling back to it on a resolver miss would just
            // silently reintroduce the bug this fix closes.
            withCovers.Add(g with { ImageUrl = coverUrl });
        }

        _logger.LogInformation(
            "Returning {Count} new games (limit={Limit}) from cache/compute",
            withCovers.Count,
            request.Limit);

        return withCovers;
    }

    private async Task<List<NewGameDto>> ComputeNewGamesAsync(
        int limit,
        CancellationToken cancellationToken)
    {
        // Soft-delete filter is enforced by HasQueryFilter on SharedGameEntity but we
        // keep the explicit predicate for read-clarity (matches Wiegers SMART intent).
        var rows = await _context.Set<SharedGameEntity>()
            .AsNoTracking()
            .Where(g => !g.IsDeleted)
            .OrderByDescending(g => g.CreatedAt)
            .Take(limit)
            .Select(g => new
            {
                g.Id,
                g.Title,
                g.YearPublished,
                g.ImageUrl,
                g.ThumbnailUrl,
                g.CreatedAt,
                Publisher = g.Publishers
                    .OrderBy(p => p.Name)
                    .Select(p => p.Name)
                    .FirstOrDefault()
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        // YearPublished defaults to 0 for legacy rows seeded without a year; the projection
        // surfaces null so the FE can render "Year unknown". Image fallback prefers ImageUrl
        // over ThumbnailUrl when both are present.
        return rows.Select(g => new NewGameDto(
            Id: g.Id,
            Name: g.Title,
            Publisher: string.IsNullOrWhiteSpace(g.Publisher) ? null : g.Publisher,
            Year: g.YearPublished > 0 ? g.YearPublished : null,
            ImageUrl: !string.IsNullOrWhiteSpace(g.ImageUrl)
                ? g.ImageUrl
                : (!string.IsNullOrWhiteSpace(g.ThumbnailUrl) ? g.ThumbnailUrl : null),
            CreatedAt: g.CreatedAt
        )).ToList();
    }
}
