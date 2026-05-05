namespace Api.Services;

/// <summary>
/// Wraps a cache invalidation operation with a bounded retry policy so that
/// transient Redis / HybridCache failures do not silently leave stale data
/// in the SharedGameCatalog read-model.
///
/// Issue #613: Cache invalidation resilience. The three SharedGameCatalog
/// event handlers call <c>HybridCache.RemoveByTagAsync</c> after upstream
/// domain events; if Redis is briefly unreachable, the in-process L1
/// eviction still happens but the L2 distributed copy keeps serving stale
/// detail/list payloads (TTL=300s) on other instances. Retry policy is
/// bounded (≤4s total) so it never blocks the MediatR notification
/// pipeline beyond the existing default.
/// </summary>
public interface ICacheInvalidationRetryPolicy
{
    /// <summary>
    /// Executes <paramref name="operation"/> with retry. Transient
    /// exceptions trigger up to 3 retries with exponential backoff
    /// (200ms → 400ms → 800ms, jittered). Non-transient exceptions
    /// (e.g. <see cref="ArgumentException"/>, <see cref="OperationCanceledException"/>
    /// when caller-cancelled) are re-thrown immediately.
    /// </summary>
    /// <param name="operation">The cache invalidation work to retry.</param>
    /// <param name="operationName">Used as a metric label and log scope.</param>
    /// <param name="ct">Caller cancellation token.</param>
    Task ExecuteAsync(
        Func<CancellationToken, ValueTask> operation,
        string operationName,
        CancellationToken ct);
}
