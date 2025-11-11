namespace Api.Services;

/// <summary>
/// Interface for rate limiting service.
/// </summary>
public interface IRateLimitService
{
    /// <summary>
    /// Check if a request is allowed under the rate limit.
    /// Uses token bucket algorithm with Redis for distributed rate limiting.
    /// </summary>
    /// <param name="key">Unique identifier for rate limit (e.g., IP address or user ID)</param>
    /// <param name="maxTokens">Maximum tokens in bucket (burst capacity)</param>
    /// <param name="refillRate">Tokens added per second</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Result with allowed status and retry-after seconds if rate limited</returns>
    Task<RateLimitResult> CheckRateLimitAsync(
        string key,
        int maxTokens,
        double refillRate,
        CancellationToken ct = default);

    /// <summary>
    /// Get rate limit configuration based on role or defaults.
    /// </summary>
    RateLimitConfig GetConfigForRole(string? role);
}
