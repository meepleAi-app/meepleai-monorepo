using Api.Models;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace Api.Services;

/// <summary>
/// Tracks query access frequency using Redis sorted sets (ZSET).
/// AI-10: Cache Optimization - Implements atomic frequency tracking with Redis ZINCRBY.
/// Provides data for dynamic TTL calculation and cache warming service.
/// Redis ZSET key format: {FrequencyTrackerKeyPrefix}{gameId} (e.g., "meepleai:freq:game-guid")
/// </summary>
public class RedisFrequencyTracker : IRedisFrequencyTracker
{
    private readonly ILogger<RedisFrequencyTracker> _logger;
    private readonly IConnectionMultiplexer _redis;
    private readonly CacheOptimizationConfiguration _config;

    /// <summary>
    /// Initializes a new instance of the RedisFrequencyTracker service.
    /// </summary>
    /// <param name="logger">Logger for diagnostic information</param>
    /// <param name="redis">Redis connection multiplexer</param>
    /// <param name="config">Cache optimization configuration (includes key prefix)</param>
    public RedisFrequencyTracker(
        ILogger<RedisFrequencyTracker> logger,
        IConnectionMultiplexer redis,
        IOptions<CacheOptimizationConfiguration> config)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _redis = redis ?? throw new ArgumentNullException(nameof(redis));
        _config = config?.Value ?? throw new ArgumentNullException(nameof(config));
    }

    /// <inheritdoc />
    public async Task TrackAccessAsync(Guid gameId, string query)
    {
        try
        {
            var db = _redis.GetDatabase();
            var redisKey = GetRedisKey(gameId);

            // Atomic increment using Redis ZINCRBY (thread-safe, no race conditions)
            await db.SortedSetIncrementAsync(redisKey, query, 1.0);

            _logger.LogDebug(
                "Incremented access count for query in game {GameId}: {Query}",
                gameId, query);
        }
        catch (RedisConnectionException ex)
        {
            // Fail-open: frequency tracking is non-critical
            _logger.LogWarning(ex, "Redis connection failed tracking access for game {GameId}", gameId);
        }
        catch (RedisTimeoutException ex)
        {
            _logger.LogWarning(ex, "Redis timeout tracking access for game {GameId}", gameId);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex, "Invalid operation tracking access for game {GameId}: {Query}", gameId, query);
        }
    }

    /// <inheritdoc />
    public async Task<List<FrequentQuery>> GetTopQueriesAsync(Guid gameId, int limit)
    {
        try
        {
            var db = _redis.GetDatabase();
            var redisKey = GetRedisKey(gameId);

            // Get top N entries by score (descending order)
            var entries = await db.SortedSetRangeByRankWithScoresAsync(
                redisKey,
                start: 0,
                stop: limit - 1,
                order: Order.Descending);

            // Convert Redis entries to FrequentQuery DTOs
            var result = entries.Select(entry => new FrequentQuery
            {
                GameId = gameId,
                Query = entry.Element.ToString(),
                AccessCount = (int)entry.Score
            }).ToList();

            _logger.LogDebug(
                "Retrieved {Count} top queries for game {GameId} (requested {Limit})",
                result.Count, gameId, limit);

            return result;
        }
        catch (RedisConnectionException ex)
        {
            _logger.LogWarning(ex, "Redis connection failed getting top queries for game {GameId}, returning empty list", gameId);
            return new List<FrequentQuery>();
        }
        catch (RedisTimeoutException ex)
        {
            _logger.LogWarning(ex, "Redis timeout getting top queries for game {GameId}, returning empty list", gameId);
            return new List<FrequentQuery>();
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex, "Invalid operation getting top queries for game {GameId}, returning empty list", gameId);
            return new List<FrequentQuery>();
        }
    }

    /// <inheritdoc />
    public async Task<int> GetFrequencyAsync(Guid gameId, string query)
    {
        try
        {
            var db = _redis.GetDatabase();
            var redisKey = GetRedisKey(gameId);

            // Get score for specific member (returns null if doesn't exist)
            var score = await db.SortedSetScoreAsync(redisKey, query);

            // Convert null to 0 (query never accessed)
            var frequency = score.HasValue ? (int)score.Value : 0;

            _logger.LogDebug(
                "Query frequency for game {GameId}, query '{Query}': {Frequency}",
                gameId, query, frequency);

            return frequency;
        }
        catch (RedisConnectionException ex)
        {
            _logger.LogWarning(ex, "Redis connection failed getting frequency for game {GameId}, returning 0", gameId);
            return 0;
        }
        catch (RedisTimeoutException ex)
        {
            _logger.LogWarning(ex, "Redis timeout getting frequency for game {GameId}, returning 0", gameId);
            return 0;
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex, "Invalid operation getting frequency for game {GameId}: {Query}, returning 0", gameId, query);
            return 0;
        }
    }

    /// <inheritdoc />
    public async Task<string> ClassifyQueryAsync(Guid gameId, string query)
    {
        var frequency = await GetFrequencyAsync(gameId, query);

        // Classification logic (boundaries are inclusive, matching DynamicTtlStrategy)
        if (frequency >= _config.HotQueryThreshold)
        {
            _logger.LogDebug(
                "Query classified as HOT (frequency={Frequency} >= {Threshold})",
                frequency, _config.HotQueryThreshold);
            return "hot";
        }

        if (frequency >= _config.WarmQueryThreshold)
        {
            _logger.LogDebug(
                "Query classified as WARM (frequency={Frequency} in range [{WarmThreshold}, {HotThreshold}))",
                frequency, _config.WarmQueryThreshold, _config.HotQueryThreshold);
            return "warm";
        }

        _logger.LogDebug(
            "Query classified as COLD (frequency={Frequency} < {Threshold})",
            frequency, _config.WarmQueryThreshold);
        return "cold";
    }

    /// <summary>
    /// Builds Redis key for frequency tracking sorted set.
    /// Format: {FrequencyTrackerKeyPrefix}{gameId}
    /// Example: "meepleai:freq:game-guid"
    /// </summary>
    private string GetRedisKey(Guid gameId)
    {
        return $"{_config.FrequencyTrackerKeyPrefix}{gameId}";
    }
}
