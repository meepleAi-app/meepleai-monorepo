using System.Threading.RateLimiting;

namespace Api.BoundedContexts.SharedGameCatalog.Infrastructure.Services;

/// <summary>
/// Single-pod in-process token bucket sized at 5 RPS to match the Wikimedia
/// published rate cap (#1823 DEC-3e). Wraps the BCL
/// <see cref="TokenBucketRateLimiter"/> so callers depend on the abstraction
/// from <see cref="IWikimediaRateLimiter"/> instead of the concrete BCL type.
/// </summary>
/// <remarks>
/// Lifetime: register as a SINGLETON via DI. The bucket has process-scoped
/// state (timestamp of last replenishment plus available token count) that
/// MUST be shared across every consumer in the AppDomain — see DEC-3e
/// rationale in <c>adr-2026-06-09-wikidata-enrichment-architecture.md</c>.
/// </remarks>
internal sealed class InMemoryWikimediaRateLimiter : IWikimediaRateLimiter, IDisposable
{
    private readonly TokenBucketRateLimiter _limiter;

    /// <summary>
    /// Default constructor: 5 RPS token bucket with unbounded queue. Used by DI.
    /// </summary>
    public InMemoryWikimediaRateLimiter()
        : this(tokenLimit: 5, tokensPerPeriod: 5, replenishmentPeriod: TimeSpan.FromSeconds(1))
    {
    }

    /// <summary>
    /// Test-friendly constructor that exposes the rate parameters. Production
    /// callers should use the parameterless ctor.
    /// </summary>
    /// <param name="tokenLimit">Maximum tokens the bucket can hold.</param>
    /// <param name="tokensPerPeriod">Tokens replenished each <paramref name="replenishmentPeriod"/>.</param>
    /// <param name="replenishmentPeriod">Period between replenishments.</param>
    internal InMemoryWikimediaRateLimiter(
        int tokenLimit,
        int tokensPerPeriod,
        TimeSpan replenishmentPeriod)
    {
        _limiter = new TokenBucketRateLimiter(new TokenBucketRateLimiterOptions
        {
            TokenLimit = tokenLimit,
            TokensPerPeriod = tokensPerPeriod,
            ReplenishmentPeriod = replenishmentPeriod,
            AutoReplenishment = true,
            QueueLimit = int.MaxValue,
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
        });
    }

    public async ValueTask AcquireAsync(CancellationToken cancellationToken = default)
    {
        using var lease = await _limiter
            .AcquireAsync(permitCount: 1, cancellationToken)
            .ConfigureAwait(false);

        if (!lease.IsAcquired)
        {
            // Queue is unbounded, so denial only happens after Dispose() — propagate
            // as an explicit error rather than silently skip a real Wikidata request.
            throw new InvalidOperationException(
                "Wikimedia rate limiter denied acquisition. Limiter may be disposed.");
        }
    }

    public void Dispose() => _limiter.Dispose();
}
