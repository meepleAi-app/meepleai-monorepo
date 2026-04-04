using Api.BoundedContexts.KnowledgeBase.Application.Queries;
using Api.Services;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.KnowledgeBase.Application.Queries;

/// <summary>
/// Handler for GetCacheStatsQuery.
/// Retrieves cache performance statistics with optional game filter.
/// </summary>
internal class GetCacheStatsQueryHandler : IQueryHandler<GetCacheStatsQuery, CacheStats>
{
    private readonly IHybridCacheService _hybridCache;
    private readonly ILogger<GetCacheStatsQueryHandler> _logger;

    public GetCacheStatsQueryHandler(
        IHybridCacheService hybridCache,
        ILogger<GetCacheStatsQueryHandler> logger)
    {
        _hybridCache = hybridCache ?? throw new ArgumentNullException(nameof(hybridCache));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<CacheStats> Handle(
        GetCacheStatsQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        _logger.LogInformation(
            "Retrieving cache statistics (GameId filter: {GameId})",
            query.GameId ?? "none");

        // Get stats from HybridCache
        var hybridStats = await _hybridCache.GetStatsAsync(cancellationToken).ConfigureAwait(false);

        var total = hybridStats.TotalHits + hybridStats.TotalMisses;
        var hitRate = total > 0 ? (double)hybridStats.TotalHits / total : 0;

        var stats = new CacheStats
        {
            TotalHits = hybridStats.TotalHits,
            TotalMisses = hybridStats.TotalMisses,
            HitRate = hitRate,
            TotalKeys = (int)hybridStats.L1EntryCount,
            CacheSizeBytes = hybridStats.L1MemoryBytes
        };

        _logger.LogInformation(
            "Cache stats retrieved: Hits={Hits}, Misses={Misses}, HitRate={HitRate:P2}",
            stats.TotalHits, stats.TotalMisses, stats.HitRate);

        return stats;
    }
}
