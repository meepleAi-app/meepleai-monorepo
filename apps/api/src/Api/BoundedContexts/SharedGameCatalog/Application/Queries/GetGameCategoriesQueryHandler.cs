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

    public GetGameCategoriesQueryHandler(MeepleAiDbContext context, HybridCache cache)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
    }

    public async Task<List<GameCategoryDto>> Handle(GetGameCategoriesQuery query, CancellationToken cancellationToken)
    {
        const string cacheKey = "game-categories";

        return await _cache.GetOrCreateAsync<List<GameCategoryDto>>(
            cacheKey,
            async cancel =>
            {
                // Issue #1440: include emoji + color for admin/filter UI surfaces.
                // GameCount is left null on the public query; admin endpoint
                // (AdminCategoriesEndpoints) issues a dedicated query that
                // computes the join count.
                var categories = await _context.GameCategories
                    .AsNoTracking()
                    .OrderBy(c => c.Name)
                    .Select(c => new GameCategoryDto(c.Id, c.Name, c.Slug, c.Emoji, c.Color, null))
                    .ToListAsync(cancel)
                    .ConfigureAwait(false);

                return categories;
            },
            new HybridCacheEntryOptions
            {
                Expiration = TimeSpan.FromHours(24)  // Categories rarely change
            },
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }
}
