using Api.Services;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands;

/// <summary>
/// Epic #3470 Slice 2 (AC-6): evicts the SharedGameCatalog read-model caches whose
/// resolved cover changes when an admin assigns or removes a per-context cover. Busts
/// the list tag (<c>search-games</c>) and the per-game detail tag
/// (<c>shared-game:{id}</c>) across replicas so the new cover renders immediately
/// instead of waiting for the 15min–2h HybridCache TTL.
///
/// The literal tags mirror <c>SearchSharedGamesQueryHandler</c> (list) and
/// <c>GetSharedGameByIdQueryHandler</c> (detail), and the invalidation pattern mirrors
/// <c>EnrichCatalogCoverCommandHandler</c>, the other cover-mutating command in this BC.
/// </summary>
internal static class CoverCacheInvalidation
{
    private const string SearchGamesTag = "search-games";

    public static async Task EvictReadModelAsync(
        IHybridCacheService cache,
        Guid gameId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(cache);

        // Cross-replica L1+L2 eviction via Redis Pub/Sub; falls back to local
        // eviction when Redis is unreachable (see IHybridCacheService docs), so a
        // transient Redis outage degrades to a per-node TTL wait rather than throwing.
        await cache.RemoveByTagAcrossReplicasAsync(SearchGamesTag, cancellationToken).ConfigureAwait(false);
        await cache.RemoveByTagAcrossReplicasAsync($"shared-game:{gameId}", cancellationToken).ConfigureAwait(false);
    }
}
