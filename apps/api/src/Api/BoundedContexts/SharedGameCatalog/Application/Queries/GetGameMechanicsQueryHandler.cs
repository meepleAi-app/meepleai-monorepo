using Api.Infrastructure;
using Api.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Queries;

/// <summary>
/// Handler for getting all game mechanics.
/// Uses HybridCache with 24h TTL (mechanics rarely change).
/// Issue #2371 Phase 2
/// </summary>
internal sealed class GetGameMechanicsQueryHandler : IRequestHandler<GetGameMechanicsQuery, List<GameMechanicDto>>
{
    private readonly MeepleAiDbContext _context;
    private readonly HybridCache _cache;
    private readonly ICacheMetricsRecorder _cacheMetrics;

    public GetGameMechanicsQueryHandler(MeepleAiDbContext context, HybridCache cache, ICacheMetricsRecorder cacheMetrics)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _cacheMetrics = cacheMetrics ?? throw new ArgumentNullException(nameof(cacheMetrics));
    }

    public async Task<List<GameMechanicDto>> Handle(GetGameMechanicsQuery query, CancellationToken cancellationToken)
    {
        const string cacheKey = "game-mechanics";
        bool cacheHit = true;

        // Try cache first (24h TTL - mechanics rarely change)
        var result = await _cache.GetOrCreateAsync<List<GameMechanicDto>>(
            cacheKey,
            async cancel =>
            {
                cacheHit = false;
                await _cacheMetrics.RecordCacheMissAsync("get_mechanics", "shared_games").ConfigureAwait(false);

                var mechanics = await _context.GameMechanics
                    .AsNoTracking()
                    .OrderBy(m => m.Name)
                    .Select(m => new GameMechanicDto(m.Id, m.Name, m.Slug))
                    .ToListAsync(cancel)
                    .ConfigureAwait(false);

                return mechanics;
            },
            new HybridCacheEntryOptions
            {
                Expiration = TimeSpan.FromHours(24)  // Mechanics rarely change
            },
            cancellationToken: cancellationToken).ConfigureAwait(false);

        if (cacheHit)
        {
            await _cacheMetrics.RecordCacheHitAsync("get_mechanics", "shared_games").ConfigureAwait(false);
        }

        return result;
    }
}
