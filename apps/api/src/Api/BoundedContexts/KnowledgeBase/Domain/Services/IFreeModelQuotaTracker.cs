using Api.BoundedContexts.KnowledgeBase.Domain.Models;

namespace Api.BoundedContexts.KnowledgeBase.Domain.Services;

/// <summary>
/// Tracks free-model rate limit events in Redis so that routing can avoid
/// exhausted models until quota resets at midnight UTC.
///
/// Issue #5087: Free model quota tracking — Redis state for RPM/RPD errors.
/// Issue #5088: Ollama fallback — RPD exhaustion triggers provider switch.
/// </summary>
internal interface IFreeModelQuotaTracker
{
    /// <summary>
    /// Record a rate limit error for a specific model.
    /// For <see cref="RateLimitErrorType.Rpd"/>, marks the model as exhausted until the
    /// UTC midnight reset indicated by <paramref name="resetTimestampMs"/>.
    /// </summary>
    Task RecordRateLimitErrorAsync(
        string modelId,
        RateLimitErrorType errorType,
        long? resetTimestampMs,
        CancellationToken ct = default);

    /// <summary>
    /// Returns <c>true</c> if the model's daily (RPD) quota is exhausted today.
    /// Used by routing to skip this model and prefer Ollama.
    /// </summary>
    Task<bool> IsRpdExhaustedAsync(string modelId, CancellationToken ct = default);

    /// <summary>
    /// Returns the last rate limit error type observed, or <c>null</c> if none.
    /// Used by the admin dashboard to surface the current error state.
    /// </summary>
    Task<RateLimitErrorType?> GetLastErrorTypeAsync(CancellationToken ct = default);

    /// <summary>
    /// Returns the UTC DateTime at which the RPD quota resets (from X-RateLimit-Reset),
    /// or <c>null</c> if not yet recorded.
    /// </summary>
    Task<DateTime?> GetRpdResetTimeAsync(CancellationToken ct = default);

    /// <summary>
    /// Issue #5476: Flush all cached quota state from Redis.
    /// Used by admin emergency controls to clear stale RPD exhaustion flags.
    /// </summary>
    Task FlushCacheAsync(CancellationToken ct = default);
}
