using Api.Infrastructure;
using Api.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Queries;

/// <summary>
/// Handler for getting all game categories.
/// Uses HybridCache with 24h TTL (categories rarely change).
/// Issue #2371 Phase 2
/// </summary>
internal sealed class GetGameCategoriesQueryHandler : IRequestHandler<GetGameCategoriesQuery, List<GameCategoryDto>>
{
    private readonly MeepleAiDbContext _context;
    private readonly HybridCache _cache;
    private readonly ICacheMetricsRecorder _cacheMetrics;

    public GetGameCategoriesQueryHandler(MeepleAiDbContext context, HybridCache cache, ICacheMetricsRecorder cacheMetrics)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _cacheMetrics = cacheMetrics ?? throw new ArgumentNullException(nameof(cacheMetrics));
    }

    public async Task<List<GameCategoryDto>> Handle(GetGameCategoriesQuery query, CancellationToken cancellationToken)
    {
        const string cacheKey = "game-categories";
        bool cacheHit = true;

        // Try cache first (24h TTL - categories rarely change)
        var result = await _cache.GetOrCreateAsync<List<GameCategoryDto>>(
            cacheKey,
            async cancel =>
            {
                cacheHit = false;
                await _cacheMetrics.RecordCacheMissAsync("get_categories", "shared_games").ConfigureAwait(false);

                var categories = await _context.GameCategories
                    .AsNoTracking()
                    .OrderBy(c => c.Name)
                    .Select(c => new GameCategoryDto(c.Id, c.Name, c.Slug))
                    .ToListAsync(cancel)
                    .ConfigureAwait(false);

                return categories;
            },
            new HybridCacheEntryOptions
            {
                Expiration = TimeSpan.FromHours(24)  // Categories rarely change
            },
            cancellationToken: cancellationToken).ConfigureAwait(false);

        if (cacheHit)
        {
            await _cacheMetrics.RecordCacheHitAsync("get_categories", "shared_games").ConfigureAwait(false);
        }

        return result;
    }
}
