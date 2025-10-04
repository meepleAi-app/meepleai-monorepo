namespace Api.Services;

/// <summary>
/// AI-05: Cache service for AI-generated responses (QA, Explain, Setup)
/// Provides Redis-backed caching to reduce latency for frequently asked questions.
/// </summary>
public interface IAiResponseCacheService
{
    /// <summary>
    /// Get cached response for a given cache key.
    /// </summary>
    /// <typeparam name="T">Type of response (QaResponse, ExplainResponse, SetupGuideResponse)</typeparam>
    /// <param name="cacheKey">Cache key</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Cached response if found, null otherwise</returns>
    Task<T?> GetAsync<T>(string cacheKey, CancellationToken ct = default) where T : class;

    /// <summary>
    /// Set cached response with TTL.
    /// </summary>
    /// <typeparam name="T">Type of response (QaResponse, ExplainResponse, SetupGuideResponse)</typeparam>
    /// <param name="cacheKey">Cache key</param>
    /// <param name="response">Response to cache</param>
    /// <param name="ttlSeconds">Time-to-live in seconds (default: 24 hours)</param>
    /// <param name="ct">Cancellation token</param>
    Task SetAsync<T>(string cacheKey, T response, int ttlSeconds = 86400, CancellationToken ct = default) where T : class;

    /// <summary>
    /// Generate cache key for QA endpoint.
    /// </summary>
    /// <param name="tenantId">Tenant ID</param>
    /// <param name="gameId">Game ID</param>
    /// <param name="query">User query</param>
    /// <returns>Cache key</returns>
    string GenerateQaCacheKey(string tenantId, string gameId, string query);

    /// <summary>
    /// Generate cache key for Explain endpoint.
    /// </summary>
    /// <param name="tenantId">Tenant ID</param>
    /// <param name="gameId">Game ID</param>
    /// <param name="topic">Topic to explain</param>
    /// <returns>Cache key</returns>
    string GenerateExplainCacheKey(string tenantId, string gameId, string topic);

    /// <summary>
    /// Generate cache key for Setup endpoint.
    /// </summary>
    /// <param name="tenantId">Tenant ID</param>
    /// <param name="gameId">Game ID</param>
    /// <returns>Cache key</returns>
    string GenerateSetupCacheKey(string tenantId, string gameId);
}
