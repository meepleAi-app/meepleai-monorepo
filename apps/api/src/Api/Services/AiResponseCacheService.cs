using System.Text;
using System.Text.Json;
using Api.Constants;
using Api.Helpers;
using Api.Infrastructure;
using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;

namespace Api.Services;

/// <summary>
/// PERF-05: HybridCache-based implementation of AI response caching.
/// Replaces direct Redis usage with HybridCache for cache stampede protection and L1+L2 tiering.
/// </summary>
public class AiResponseCacheService : IAiResponseCacheService
{
    private readonly IHybridCacheService _hybridCache;
    private readonly MeepleAiDbContext _dbContext;
    private readonly ILogger<AiResponseCacheService> _logger;

    public AiResponseCacheService(
        IHybridCacheService hybridCache,
        MeepleAiDbContext dbContext,
        ILogger<AiResponseCacheService> logger)
    {
        _hybridCache = hybridCache ?? throw new ArgumentNullException(nameof(hybridCache));
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    /// <remarks>
    /// PERF-05: This method is now a thin wrapper around HybridCache.
    /// For new code, prefer using IHybridCacheService.GetOrCreateAsync directly.
    /// </remarks>
    public async Task<T?> GetAsync<T>(string cacheKey, CancellationToken ct = default) where T : class
    {
        try
        {
            // Try to get from HybridCache (no factory, so returns null if not found)
            // Note: HybridCache doesn't have a simple Get operation, so we simulate it
            // by using GetOrCreateAsync with a factory that returns null
            var result = await _hybridCache.GetOrCreateAsync(
                cacheKey: cacheKey,
                factory: _ => Task.FromResult((T?)null!),
                tags: null,
                expiration: null,
                ct: ct
            );

            if (result == null)
            {
                _logger.LogDebug("Cache miss for key: {CacheKey}", cacheKey);
            }
            else
            {
                _logger.LogInformation("Cache hit for key: {CacheKey}", cacheKey);
            }

            return result;
        }
        catch (RedisConnectionException ex)
        {
            _logger.LogWarning(ex, "Redis connection failed for key {CacheKey}. Proceeding without cache.", cacheKey);
            return null; // Fail-open: cache miss
        }
        catch (RedisTimeoutException ex)
        {
            _logger.LogWarning(ex, "Redis timeout for key {CacheKey}. Proceeding without cache.", cacheKey);
            return null;
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "JSON deserialization error for key {CacheKey}. Proceeding without cache.", cacheKey);
            return null;
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Invalid cache operation for key {CacheKey}. Proceeding without cache.", cacheKey);
            return null;
        }
    }

    /// <inheritdoc />
    /// <remarks>
    /// PERF-05: This method is now a thin wrapper around HybridCache.
    /// For new code, prefer using IHybridCacheService.GetOrCreateAsync directly
    /// with a factory function to get cache stampede protection.
    /// Tags are automatically extracted from cache key for tag-based invalidation.
    /// </remarks>
    public async Task SetAsync<T>(string cacheKey, T response, int ttlSeconds = TimeConstants.DefaultAiResponseCacheTtlSeconds, CancellationToken ct = default) where T : class
    {
        try
        {
            // Extract tags from cache key for tag-based invalidation
            // Cache key format: "meepleai:{endpoint}:{gameId}:{hash}"
            var tags = ExtractTagsFromCacheKey(cacheKey);

            // Use GetOrCreateAsync to ensure value is cached with tags
            await _hybridCache.GetOrCreateAsync(
                cacheKey: cacheKey,
                factory: _ => Task.FromResult(response),
                tags: tags,
                expiration: TimeSpan.FromSeconds(ttlSeconds),
                ct: ct
            );

            _logger.LogInformation("Cached response for key: {CacheKey} with tags: {Tags} (TTL: {TTL}s)",
                cacheKey, tags != null ? string.Join(", ", tags) : "none", ttlSeconds);
        }
        catch (RedisConnectionException ex)
        {
            _logger.LogWarning(ex, "Redis connection failed setting key {CacheKey}. Proceeding without cache.", cacheKey);
            // Fail-open: cache write failure is non-critical
        }
        catch (RedisTimeoutException ex)
        {
            _logger.LogWarning(ex, "Redis timeout setting key {CacheKey}. Proceeding without cache.", cacheKey);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "JSON serialization error for key {CacheKey}. Proceeding without cache.", cacheKey);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Invalid cache operation for key {CacheKey}. Proceeding without cache.", cacheKey);
        }
    }

    /// <summary>
    /// Extract tags from cache key for tag-based invalidation.
    /// Cache key format: "meepleai:{endpoint}:{gameId}:{hash}"
    /// Returns tags: ["game:{gameId}", "endpoint:{endpoint}"]
    /// </summary>
    private static string[]? ExtractTagsFromCacheKey(string cacheKey)
    {
        try
        {
            // Expected format: "meepleai:{endpoint}:{gameId}:{hash}"
            var parts = cacheKey.Split(':');

            if (parts.Length < 3)
            {
                return null; // Invalid cache key format
            }

            var endpoint = parts[1]; // "qa", "explain", "setup"
            var gameId = parts[2];

            return new[]
            {
                $"game:{gameId}",
                $"endpoint:{endpoint}"
            };
        }
        catch
        {
            // If tag extraction fails, cache without tags (degraded but functional)
            return null;
        }
    }

    /// <inheritdoc />
    public string GenerateQaCacheKey(string gameId, string query)
    {
        var queryHash = CryptographyHelper.ComputeSha256Hash(query.Trim().ToLowerInvariant());
        return $"meepleai:qa:{gameId}:{queryHash}";
    }

    /// <inheritdoc />
    public string GenerateExplainCacheKey(string gameId, string topic)
    {
        var topicHash = CryptographyHelper.ComputeSha256Hash(topic.Trim().ToLowerInvariant());
        return $"meepleai:explain:{gameId}:{topicHash}";
    }

    /// <inheritdoc />
    public string GenerateSetupCacheKey(string gameId)
    {
        return $"meepleai:setup:{gameId}";
    }

    /// <inheritdoc />
    public async Task InvalidateGameAsync(string gameId, CancellationToken ct = default)
    {
        var tag = $"game:{gameId}";
        var removed = await _hybridCache.RemoveByTagAsync(tag, ct).ConfigureAwait(false);
        _logger.LogInformation("Invalidated {Count} cache entries for game: {GameId}", removed, gameId);
    }

    /// <inheritdoc />
    public async Task InvalidateEndpointAsync(string gameId, AiCacheEndpoint endpoint, CancellationToken ct = default)
    {
        var endpointName = endpoint.ToString().ToLowerInvariant();
        var tags = new[] { $"game:{gameId}", $"endpoint:{endpointName}" };
        var removed = await _hybridCache.RemoveByTagsAsync(tags, ct).ConfigureAwait(false);
        _logger.LogInformation(
            "Invalidated {Count} cache entries for game {GameId}, endpoint {Endpoint}",
            removed, gameId, endpoint);
    }

    /// <inheritdoc />
    public async Task InvalidateByCacheTagAsync(string tag, CancellationToken ct = default)
    {
        var removed = await _hybridCache.RemoveByTagAsync(tag, ct).ConfigureAwait(false);
        _logger.LogInformation("Invalidated {Count} cache entries for tag: {Tag}", removed, tag);
    }

    /// <inheritdoc />
    public async Task<CacheStats> GetCacheStatsAsync(string? gameId = null, CancellationToken ct = default)
    {
        // PERF-05: Simplified implementation using HybridCache stats
        // Advanced DB-based stats tracking can be added later if needed
        var hybridStats = await _hybridCache.GetStatsAsync(ct).ConfigureAwait(false);

        var total = hybridStats.TotalHits + hybridStats.TotalMisses;
        var hitRate = total > 0 ? (double)hybridStats.TotalHits / total : 0;

        return new CacheStats
        {
            TotalHits = hybridStats.TotalHits,
            TotalMisses = hybridStats.TotalMisses,
            HitRate = hitRate,
            TotalKeys = (int)hybridStats.L1EntryCount,
            CacheSizeBytes = hybridStats.L1MemoryBytes,
            TopQuestions = new List<TopQuestion>() // FUTURE: Track most frequently cached questions for analytics
        };
    }

    /// <inheritdoc />
    public Task RecordCacheAccessAsync(string gameId, string questionHash, bool isHit, CancellationToken ct = default)
    {
        // PERF-05: HybridCache tracks hits/misses internally, no need for manual recording
        // This method is kept for interface compatibility but does nothing
        _logger.LogDebug("Cache access recording is handled by HybridCache internally");
        return Task.CompletedTask;
    }

}
