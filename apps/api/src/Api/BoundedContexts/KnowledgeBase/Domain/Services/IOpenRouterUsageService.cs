using Api.BoundedContexts.KnowledgeBase.Domain.Models;

namespace Api.BoundedContexts.KnowledgeBase.Domain.Services;

/// <summary>
/// Issue #5074: Service that maintains a Redis cache of OpenRouter account status,
/// rate limits, and daily spend. Backed by a BackgroundService that polls /auth/key every 60s.
/// </summary>
public interface IOpenRouterUsageService
{
    /// <summary>
    /// Returns the last polled OpenRouter account status (balance, usage, rate limits).
    /// Returns null if Redis is unavailable or the cache has not been populated yet.
    /// </summary>
    Task<OpenRouterAccountStatus?> GetAccountStatusAsync(CancellationToken ct = default);

    /// <summary>
    /// Returns the accumulated API spend for today (UTC) in USD.
    /// Returns 0 if Redis is unavailable or no requests have been recorded today.
    /// </summary>
    Task<decimal> GetDailySpendAsync(CancellationToken ct = default);

    /// <summary>
    /// Records the cost of a completed request to the daily spend accumulator in Redis.
    /// Fire-and-forget safe: silently ignores Redis errors.
    /// </summary>
    Task RecordRequestCostAsync(decimal costUsd, CancellationToken ct = default);
}
