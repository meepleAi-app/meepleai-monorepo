namespace Api.BoundedContexts.SharedGameCatalog.Infrastructure.Services;

/// <summary>
/// Coordinates the 5 RPS hard cap that Wikimedia publishes for both the Wikidata
/// SPARQL endpoint and the Commons API. Shared between
/// <c>WikidataCatalogProvider.FetchCoverImageAsync</c> and the future
/// <c>IWikimediaCommonsClient</c> so a single token bucket counts against
/// requests to the umbrella host (<c>*.wikimedia.org</c>).
/// </summary>
/// <remarks>
/// <para>
/// Implementation choice (DEC-3e, locked sess.46h post-spike): single-pod
/// in-process token bucket. The Wikidata catalog spike measured 3.3h per
/// 30k-game pass at 5 RPS — well within the 24h CRON window — so the batch
/// runs with <c>HPA.minReplicas=HPA.maxReplicas=1</c> and a distributed
/// (Redis-backed) limiter is not required. Once multi-pod is needed, swap in
/// a <c>RedisRateLimiter</c> behind the same interface.
/// </para>
/// <para>
/// DEC-3b separation rationale: <c>WikidataCatalogProvider</c> and the future
/// <c>IWikimediaCommonsClient</c> are intentionally kept as separate clients
/// (different bounded responsibilities — catalog seed vs image fetch) but both
/// inject this rate limiter so they cannot drift apart and accidentally burst
/// past 5 RPS combined.
/// </para>
/// </remarks>
internal interface IWikimediaRateLimiter
{
    /// <summary>
    /// Waits asynchronously until a 5 RPS token is available, then consumes it.
    /// Throws when the bucket is disposed or the cancellation token fires.
    /// </summary>
    ValueTask AcquireAsync(CancellationToken cancellationToken = default);
}
