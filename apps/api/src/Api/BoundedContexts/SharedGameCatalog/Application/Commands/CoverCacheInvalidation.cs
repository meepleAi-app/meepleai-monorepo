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
/// <c>GetSharedGameByIdQueryHandler</c> (detail). Each eviction runs through
/// <see cref="ICacheInvalidationRetryPolicy"/> (issue #613) so a transient Redis blip
/// is retried rather than silently leaving the L2 copy stale — mirroring
/// <c>EnrichCatalogCoverCommandHandler</c>, the other cover-mutating command in this BC.
/// </summary>
internal static class CoverCacheInvalidation
{
    private const string SearchGamesTag = "search-games";

    public static async Task EvictReadModelAsync(
        IHybridCacheService cache,
        ICacheInvalidationRetryPolicy retryPolicy,
        Guid gameId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(cache);
        ArgumentNullException.ThrowIfNull(retryPolicy);

        await retryPolicy.ExecuteAsync(
            token => new ValueTask(cache.RemoveByTagAcrossReplicasAsync(SearchGamesTag, token)),
            "shared-games.list",
            cancellationToken).ConfigureAwait(false);

        await retryPolicy.ExecuteAsync(
            token => new ValueTask(cache.RemoveByTagAcrossReplicasAsync($"shared-game:{gameId}", token)),
            "shared-games.detail",
            cancellationToken).ConfigureAwait(false);
    }
}
