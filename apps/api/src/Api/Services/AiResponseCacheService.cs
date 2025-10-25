using System.Security.Cryptography;
using System.Text;
using Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

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
                factory: async _ => (T?)null!,
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
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Cache get failed for key {CacheKey}. Proceeding without cache.", cacheKey);
            return null; // Fail gracefully
        }
    }

    /// <inheritdoc />
    /// <remarks>
    /// PERF-05: This method is now a thin wrapper around HybridCache.
    /// For new code, prefer using IHybridCacheService.GetOrCreateAsync directly
    /// with a factory function to get cache stampede protection.
    /// </remarks>
    public async Task SetAsync<T>(string cacheKey, T response, int ttlSeconds = 86400, CancellationToken ct = default) where T : class
    {
        try
        {
            // Use GetOrCreateAsync to ensure value is cached
            await _hybridCache.GetOrCreateAsync(
                cacheKey: cacheKey,
                factory: async _ => response,
                tags: null,
                expiration: TimeSpan.FromSeconds(ttlSeconds),
                ct: ct
            );

            _logger.LogInformation("Cached response for key: {CacheKey} (TTL: {TTL}s)", cacheKey, ttlSeconds);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Cache set failed for key {CacheKey}. Proceeding without cache.", cacheKey);
            // Fail gracefully - don't throw
        }
    }

    /// <inheritdoc />
    public string GenerateQaCacheKey(string gameId, string query)
    {
        var queryHash = ComputeSha256Hash(query.Trim().ToLowerInvariant());
        return $"meepleai:qa:{gameId}:{queryHash}";
    }

    /// <inheritdoc />
    public string GenerateExplainCacheKey(string gameId, string topic)
    {
        var topicHash = ComputeSha256Hash(topic.Trim().ToLowerInvariant());
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
        var removed = await _hybridCache.RemoveByTagAsync(tag, ct);
        _logger.LogInformation("Invalidated {Count} cache entries for game: {GameId}", removed, gameId);
    }

    /// <inheritdoc />
    public async Task InvalidateEndpointAsync(string gameId, AiCacheEndpoint endpoint, CancellationToken ct = default)
    {
        var endpointName = endpoint.ToString().ToLowerInvariant();
        var tags = new[] { $"game:{gameId}", $"endpoint:{endpointName}" };
        var removed = await _hybridCache.RemoveByTagsAsync(tags, ct);
        _logger.LogInformation(
            "Invalidated {Count} cache entries for game {GameId}, endpoint {Endpoint}",
            removed, gameId, endpoint);
    }

    /// <inheritdoc />
    public async Task InvalidateByCacheTagAsync(string tag, CancellationToken ct = default)
    {
        var removed = await _hybridCache.RemoveByTagAsync(tag, ct);
        _logger.LogInformation("Invalidated {Count} cache entries for tag: {Tag}", removed, tag);
    }

    /// <inheritdoc />
    public async Task<CacheStats> GetCacheStatsAsync(string? gameId = null, CancellationToken ct = default)
    {
        // PERF-05: Simplified implementation using HybridCache stats
        // Advanced DB-based stats tracking can be added later if needed
        var hybridStats = await _hybridCache.GetStatsAsync(ct);

        var total = hybridStats.TotalHits + hybridStats.TotalMisses;
        var hitRate = total > 0 ? (double)hybridStats.TotalHits / total : 0;

        return new CacheStats
        {
            TotalHits = hybridStats.TotalHits,
            TotalMisses = hybridStats.TotalMisses,
            HitRate = hitRate,
            TotalKeys = (int)hybridStats.L1EntryCount,
            CacheSizeBytes = hybridStats.L1MemoryBytes,
            TopQuestions = new List<TopQuestion>() // TODO: Implement if needed
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

    private static string ComputeSha256Hash(string input)
    {
        var bytes = Encoding.UTF8.GetBytes(input);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
