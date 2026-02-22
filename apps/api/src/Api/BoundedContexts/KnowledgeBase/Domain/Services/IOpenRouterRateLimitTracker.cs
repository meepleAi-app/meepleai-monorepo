using Api.BoundedContexts.KnowledgeBase.Domain.Models;

namespace Api.BoundedContexts.KnowledgeBase.Domain.Services;

/// <summary>
/// Issue #5075: Tracks RPM and TPM for OpenRouter providers using Redis sliding windows.
/// Allows HybridLlmService to detect approaching rate limits before receiving a 429.
/// </summary>
public interface IOpenRouterRateLimitTracker
{
    /// <summary>
    /// Records a completed request in the sliding window for the given provider.
    /// </summary>
    Task RecordRequestAsync(string provider, string modelId, int totalTokens, CancellationToken ct = default);

    /// <summary>
    /// Returns the current RPM/TPM utilization for the given provider.
    /// Returns a zero-valued status if Redis is unavailable.
    /// </summary>
    Task<RateLimitStatus> GetCurrentStatusAsync(string provider, CancellationToken ct = default);

    /// <summary>
    /// Returns true if the provider's RPM utilization exceeds the given threshold percentage.
    /// Always returns false if Redis is unavailable or the limit is unknown.
    /// </summary>
    Task<bool> IsApproachingLimitAsync(string provider, int thresholdPercent = 80, CancellationToken ct = default);
}
