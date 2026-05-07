using Api.Infrastructure;
using Api.Infrastructure.Entities.SharedGameCatalog;
using Api.Services;
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
    private readonly ILogger<GetNewGamesQueryHandler> _logger;

    public GetNewGamesQueryHandler(
        MeepleAiDbContext context,
        IHybridCacheService cache,
        ILogger<GetNewGamesQueryHandler> logger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
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

        var trimmed = allNew.Take(request.Limit).ToArray();

        _logger.LogInformation(
            "Returning {Count} new games (limit={Limit}) from cache/compute",
            trimmed.Length,
            request.Limit);

        return trimmed;
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
