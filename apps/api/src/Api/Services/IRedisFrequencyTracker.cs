

#pragma warning disable MA0048 // File name must match type name - Contains Interface with supporting types
namespace Api.Services;

/// <summary>
/// Tracks query access frequency using Redis sorted sets (ZSET).
/// AI-10: Cache Optimization - Provides data for dynamic TTL calculation and cache warming.
/// Uses atomic Redis ZINCRBY for concurrent-safe frequency tracking.
/// </summary>
internal interface IRedisFrequencyTracker
{
    /// <summary>
    /// Increments access count for a query by 1 using Redis ZINCRBY.
    /// Redis key format: {FrequencyTrackerKeyPrefix}{gameId}
    /// Atomically increments score in sorted set (no race conditions).
    /// </summary>
    /// <param name="gameId">Game ID for query isolation</param>
    /// <param name="query">Query text (used as ZSET member)</param>
    Task TrackAccessAsync(Guid gameId, string query);

    /// <summary>
    /// Retrieves top N queries by access frequency (descending order).
    /// Used by cache warming service to identify hot queries for pre-caching.
    /// Returns empty list if no queries tracked for the game.
    /// </summary>
    /// <param name="gameId">Game ID for query filtering</param>
    /// <param name="limit">Maximum number of queries to return</param>
    /// <returns>List of queries with access counts, ordered by frequency (highest first)</returns>
    Task<List<FrequentQuery>> GetTopQueriesAsync(Guid gameId, int limit);

    /// <summary>
    /// Gets the access count (hit count) for a specific query.
    /// Returns 0 if query has never been accessed.
    /// Used by dynamic TTL strategy to classify queries.
    /// </summary>
    /// <param name="gameId">Game ID</param>
    /// <param name="query">Query text</param>
    /// <returns>Hit count (0 if query doesn't exist in tracker)</returns>
    Task<int> GetFrequencyAsync(Guid gameId, string query);

    /// <summary>
    /// Classifies a query as "hot", "warm", or "cold" based on access frequency.
    /// Uses configured thresholds (HotQueryThreshold, WarmQueryThreshold).
    /// </summary>
    /// <param name="gameId">Game ID</param>
    /// <param name="query">Query text</param>
    /// <returns>"hot", "warm", or "cold" classification</returns>
    Task<string> ClassifyQueryAsync(Guid gameId, string query);
}

/// <summary>
/// Represents a frequently accessed query with its access count.
/// Used by GetTopQueriesAsync to return ranked query list.
/// </summary>
internal class FrequentQuery
{
    /// <summary>
    /// Game ID the query belongs to
    /// </summary>
    public Guid GameId { get; set; }

    /// <summary>
    /// Query text
    /// </summary>
    public string Query { get; set; } = string.Empty;

    /// <summary>
    /// Number of times the query has been accessed
    /// </summary>
    public int AccessCount { get; set; }
}
