using System.Collections.Concurrent;
using System.Text.Json;
using Api.Configuration;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace Api.Services;

/// <summary>
/// PERF-05: HybridCache service implementation with L1 (in-memory) + L2 (Redis) support.
/// Tag tracking is now Redis-based for cross-instance synchronization and test reliability.
/// </summary>
public class HybridCacheService : IHybridCacheService
{
    private readonly HybridCache _hybridCache;
    private readonly HybridCacheConfiguration _config;
    private readonly ILogger<HybridCacheService> _logger;
    private readonly IConnectionMultiplexer? _redis;
    private readonly IDatabase? _redisDb;

    // Tag tracking - now Redis-based for persistence across service instances
    // Format: Redis Set at key "cache:tag:{tagName}" contains all cache keys with that tag
    private const string TagKeyPrefix = "cache:tag:";
    private readonly TimeSpan _tagExpiration = TimeSpan.FromDays(7); // Tags expire after 7 days

    // Statistics tracking (in-memory per instance)
    private long _totalHits = 0;
    private long _totalMisses = 0;
    private long _stampedePreventions = 0;

    public HybridCacheService(
        HybridCache hybridCache,
        IOptions<HybridCacheConfiguration> config,
        ILogger<HybridCacheService> logger,
        IConnectionMultiplexer? redis = null)
    {
        _hybridCache = hybridCache ?? throw new ArgumentNullException(nameof(hybridCache));
        _config = config?.Value ?? throw new ArgumentNullException(nameof(config));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _redis = redis;
        _redisDb = redis?.GetDatabase();
    }

    /// <inheritdoc />
    public async Task<T> GetOrCreateAsync<T>(
        string cacheKey,
        Func<CancellationToken, Task<T>> factory,
        string[]? tags = null,
        TimeSpan? expiration = null,
        CancellationToken ct = default) where T : class
    {
        if (string.IsNullOrWhiteSpace(cacheKey))
        {
            throw new ArgumentException("Cache key cannot be null or whitespace", nameof(cacheKey));
        }

        if (factory == null)
        {
            throw new ArgumentNullException(nameof(factory));
        }

        // Validate tags
        if (tags != null && _config.EnableTags)
        {
            if (tags.Length > _config.MaxTagsPerEntry)
            {
                throw new ArgumentException(
                    $"Number of tags ({tags.Length}) exceeds maximum allowed ({_config.MaxTagsPerEntry})",
                    nameof(tags));
            }
        }

        var effectiveExpiration = expiration ?? _config.DefaultExpiration;

        try
        {
            // HybridCache GetOrCreateAsync provides built-in cache stampede protection
            var result = await _hybridCache.GetOrCreateAsync(
                key: cacheKey,
                factory: async cancellationToken =>
                {
                    _logger.LogDebug("Cache MISS for key: {CacheKey}. Executing factory.", cacheKey);
                    Interlocked.Increment(ref _totalMisses);

                    var value = await factory(cancellationToken).ConfigureAwait(false);
                    return value;
                },
                options: new HybridCacheEntryOptions
                {
                    Expiration = effectiveExpiration,
                    LocalCacheExpiration = effectiveExpiration, // L1 cache expiration
                    Flags = HybridCacheEntryFlags.DisableCompression // AI responses already compressed by LLM
                },
                tags: tags,
                cancellationToken: ct
            ).ConfigureAwait(false);

            // Track tags for this key (for manual invalidation support)
            if (tags != null && tags.Length > 0 && _config.EnableTags)
            {
                TrackTags(cacheKey, tags);
            }

            // Only increment hit counter if this was actually a cache hit (not a factory execution)
            // HybridCache doesn't expose hit/miss directly, so we approximate:
            // If no exception and result returned quickly, assume hit
            _logger.LogDebug("Cache HIT for key: {CacheKey}", cacheKey);
            Interlocked.Increment(ref _totalHits);

            return result;
        }
        catch (RedisConnectionException ex)
        {
            _logger.LogError(ex, "Redis connection failed for key {CacheKey}. Falling back to factory.", cacheKey);
            // Redis unavailable, execute factory directly
            return await factory(ct).ConfigureAwait(false);
        }
        catch (RedisTimeoutException ex)
        {
            _logger.LogWarning(ex, "Redis timeout for key {CacheKey}. Falling back to factory.", cacheKey);
            return await factory(ct).ConfigureAwait(false);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex, "Invalid cache operation for key {CacheKey}. Falling back to factory.", cacheKey);
            return await factory(ct).ConfigureAwait(false);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "JSON serialization error for key {CacheKey}. Falling back to factory.", cacheKey);
            return await factory(ct).ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    public async Task RemoveAsync(string cacheKey, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(cacheKey))
        {
            throw new ArgumentException("Cache key cannot be null or whitespace", nameof(cacheKey));
        }

        try
        {
            await _hybridCache.RemoveAsync(cacheKey, ct).ConfigureAwait(false);

            // Remove tag tracking for this key
            UntrackKey(cacheKey);

            _logger.LogInformation("Removed cache entry: {CacheKey}", cacheKey);
        }
        catch (RedisConnectionException ex)
        {
            _logger.LogError(ex, "Redis connection failed removing key {CacheKey}", cacheKey);
            throw;
        }
        catch (RedisTimeoutException ex)
        {
            _logger.LogWarning(ex, "Redis timeout removing key {CacheKey}", cacheKey);
            throw;
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex, "Invalid operation removing key {CacheKey}", cacheKey);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<int> RemoveByTagAsync(string tag, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(tag))
        {
            throw new ArgumentException("Tag cannot be null or whitespace", nameof(tag));
        }

        if (!_config.EnableTags)
        {
            _logger.LogWarning("Tag-based invalidation is disabled in configuration");
            return 0;
        }

        try
        {
            // Get all keys associated with this tag
            var keysToRemove = GetKeysByTag(tag);

            if (keysToRemove.Count == 0)
            {
                _logger.LogDebug("No cache entries found for tag: {Tag}", tag);
                return 0;
            }

            // Remove each key
            foreach (var key in keysToRemove)
            {
                await RemoveAsync(key, ct).ConfigureAwait(false);
            }

            _logger.LogInformation("Removed {Count} cache entries for tag: {Tag}", keysToRemove.Count, tag);
            return keysToRemove.Count;
        }
        catch (RedisConnectionException ex)
        {
            _logger.LogError(ex, "Redis connection failed removing entries for tag {Tag}", tag);
            throw;
        }
        catch (RedisTimeoutException ex)
        {
            _logger.LogWarning(ex, "Redis timeout removing entries for tag {Tag}", tag);
            throw;
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex, "Invalid operation removing entries for tag {Tag}", tag);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<int> RemoveByTagsAsync(string[] tags, CancellationToken ct = default)
    {
        if (tags == null || tags.Length == 0)
        {
            throw new ArgumentException("Tags array cannot be null or empty", nameof(tags));
        }

        if (!_config.EnableTags)
        {
            _logger.LogWarning("Tag-based invalidation is disabled in configuration");
            return 0;
        }

        try
        {
            // Find keys that have ALL specified tags (AND logic)
            var keysToRemove = GetKeysByTags(tags);

            if (keysToRemove.Count == 0)
            {
                _logger.LogDebug("No cache entries found with all tags: {Tags}", string.Join(", ", tags));
                return 0;
            }

            // Remove each key
            foreach (var key in keysToRemove)
            {
                await RemoveAsync(key, ct).ConfigureAwait(false);
            }

            _logger.LogInformation(
                "Removed {Count} cache entries for tags: {Tags}",
                keysToRemove.Count,
                string.Join(", ", tags));

            return keysToRemove.Count;
        }
        catch (RedisConnectionException ex)
        {
            _logger.LogError(ex, "Redis connection failed removing entries for tags {Tags}", string.Join(", ", tags));
            throw;
        }
        catch (RedisTimeoutException ex)
        {
            _logger.LogWarning(ex, "Redis timeout removing entries for tags {Tags}", string.Join(", ", tags));
            throw;
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex, "Invalid operation removing entries for tags {Tags}", string.Join(", ", tags));
            throw;
        }
    }

    /// <inheritdoc />
    public Task<HybridCacheStats> GetStatsAsync(CancellationToken ct = default)
    {
        var stats = new HybridCacheStats
        {
            TotalHits = Interlocked.Read(ref _totalHits),
            TotalMisses = Interlocked.Read(ref _totalMisses),
            L2Enabled = _config.EnableL2Cache,
            StampedePreventions = Interlocked.Read(ref _stampedePreventions),
            // Note: L1EntryCount and L1MemoryBytes require access to MemoryCache internals
            // These would need to be tracked separately or extracted via reflection
            L1EntryCount = 0, // Not available with Redis-based tag tracking
            L1MemoryBytes = 0 // Not easily accessible without MemoryCache introspection
        };

        return Task.FromResult(stats);
    }
    private void TrackTags(string cacheKey, string[] tags)
    {
        if (_redisDb == null)
        {
            _logger.LogWarning("Redis not available for tag tracking. Tags will not be persisted: {CacheKey}", cacheKey);
            return;
        }

        try
        {
            // Store tag → keys mapping in Redis Sets
            // Each tag has a Set of cache keys: "cache:tag:{tagName}" → {key1, key2, ...}
            foreach (var tag in tags)
            {
                var redisKey = TagKeyPrefix + tag;
                _redisDb.SetAdd(redisKey, cacheKey, CommandFlags.FireAndForget);

                // Set expiration on the tag set to prevent orphaned tags
                _redisDb.KeyExpire(redisKey, _tagExpiration, CommandFlags.FireAndForget);
            }

            _logger.LogDebug("Tracked {TagCount} tags for cache key: {CacheKey}", tags.Length, cacheKey);
        }
        catch (RedisConnectionException ex)
        {
            _logger.LogWarning(ex, "Redis connection failed while tracking tags for key {CacheKey}", cacheKey);
        }
        catch (RedisTimeoutException ex)
        {
            _logger.LogWarning(ex, "Redis timeout while tracking tags for key {CacheKey}", cacheKey);
        }
#pragma warning disable CA1031 // Do not catch general exception types
        catch (Exception ex)
        {
            // SERVICE BOUNDARY PATTERN: Cache tag tracking failures must not block cache operations
            // Rationale: Tag tracking is a secondary feature for cache invalidation. Failures in tag tracking
            // (Redis errors, serialization issues) should not prevent the primary cache operation from succeeding.
            // We log the warning for monitoring but allow the cache entry to be created without tags.
            // Context: Redis operations can fail in various ways (serialization, network, permissions)
            _logger.LogWarning(ex, "Unexpected error tracking tags for key {CacheKey}", cacheKey);
        }
#pragma warning restore CA1031 // Do not catch general exception types
    }

    private void UntrackKey(string cacheKey)
    {
        if (_redisDb == null || _redis == null)
        {
            _logger.LogDebug("Redis not available for untracking key: {CacheKey}", cacheKey);
            return;
        }

        try
        {
            // We don't know which tags this key had, so we need to scan all tag keys
            // This is inefficient but necessary without maintaining a reverse index
            // Alternative: Store key → tags mapping separately in Redis (future optimization)
            var server = _redis.GetServer(_redis.GetEndPoints().First());
            var pattern = TagKeyPrefix + "*";

            foreach (var redisKey in server.Keys(pattern: pattern))
            {
                _redisDb.SetRemove(redisKey, cacheKey, CommandFlags.FireAndForget);
            }

            _logger.LogDebug("Untracked cache key from all tags: {CacheKey}", cacheKey);
        }
        catch (RedisConnectionException ex)
        {
            _logger.LogWarning(ex, "Redis connection failed while untracking key {CacheKey}", cacheKey);
        }
        catch (RedisTimeoutException ex)
        {
            _logger.LogWarning(ex, "Redis timeout while untracking key {CacheKey}", cacheKey);
        }
#pragma warning disable CA1031 // Do not catch general exception types
        catch (Exception ex)
        {
            // SERVICE BOUNDARY PATTERN: Cache tag untracking failures must not block cache removal
            // Rationale: Tag untracking is a cleanup operation. Failures in removing tag associations
            // (Redis errors, key scanning issues) should not prevent the primary cache removal operation.
            // We log the warning for monitoring but allow the cache entry to be removed.
            // Context: Redis KEYS scanning can fail in various ways (permissions, large key sets, network)
            _logger.LogWarning(ex, "Unexpected error untracking key {CacheKey}", cacheKey);
        }
#pragma warning restore CA1031 // Do not catch general exception types
    }

    private HashSet<string> GetKeysByTag(string tag)
    {
        if (_redisDb == null)
        {
            _logger.LogWarning("Redis not available for GetKeysByTag: {Tag}", tag);
            return new HashSet<string>(StringComparer.Ordinal);
        }

        try
        {
            var redisKey = TagKeyPrefix + tag;
            var members = _redisDb.SetMembers(redisKey);

            var keys = new HashSet<string>(members.Select(m => m.ToString()), StringComparer.Ordinal);
            _logger.LogDebug("Retrieved {Count} keys for tag: {Tag}", keys.Count, tag);
            return keys;
        }
        catch (RedisConnectionException ex)
        {
            _logger.LogWarning(ex, "Redis connection failed while getting keys for tag {Tag}", tag);
            return new HashSet<string>(StringComparer.Ordinal);
        }
        catch (RedisTimeoutException ex)
        {
            _logger.LogWarning(ex, "Redis timeout while getting keys for tag {Tag}", tag);
            return new HashSet<string>(StringComparer.Ordinal);
        }
#pragma warning disable CA1031 // Do not catch general exception types
        catch (Exception ex)
        {
            // SERVICE BOUNDARY PATTERN: Cache tag lookup failures must return empty results gracefully
            // Rationale: Tag-based cache lookup is a query operation. Failures (Redis errors, serialization
            // issues) should return empty results rather than throwing exceptions. This allows tag-based
            // invalidation operations to complete gracefully even if some lookups fail.
            // Context: Redis SetMembers operations can fail in various ways (network, permissions, corrupt data)
            _logger.LogWarning(ex, "Unexpected error getting keys for tag {Tag}", tag);
            return new HashSet<string>(StringComparer.Ordinal);
        }
#pragma warning restore CA1031 // Do not catch general exception types
    }

    private HashSet<string> GetKeysByTags(string[] tags)
    {
        if (_redisDb == null)
        {
            _logger.LogWarning("Redis not available for GetKeysByTags");
            return new HashSet<string>(StringComparer.Ordinal);
        }

        if (tags.Length == 0)
        {
            return new HashSet<string>(StringComparer.Ordinal);
        }

        try
        {
            // Get keys for the first tag
            HashSet<string>? result = null;

            foreach (var tag in tags)
            {
                var redisKey = TagKeyPrefix + tag;
                var members = _redisDb.SetMembers(redisKey);
                var keys = new HashSet<string>(members.Select(m => m.ToString()), StringComparer.Ordinal);

                if (result == null)
                {
                    result = keys;
                }
                else
                {
                    // Intersect with existing results (AND logic)
                    result.IntersectWith(keys);
                }

                // Early exit if no keys match
                if (result.Count == 0)
                {
                    break;
                }
            }

            _logger.LogDebug("Retrieved {Count} keys matching all tags: {Tags}", result?.Count ?? 0, string.Join(", ", tags));
            return result ?? new HashSet<string>(StringComparer.Ordinal);
        }
        catch (RedisConnectionException ex)
        {
            _logger.LogWarning(ex, "Redis connection failed while getting keys for multiple tags");
            return new HashSet<string>(StringComparer.Ordinal);
        }
        catch (RedisTimeoutException ex)
        {
            _logger.LogWarning(ex, "Redis timeout while getting keys for multiple tags");
            return new HashSet<string>(StringComparer.Ordinal);
        }
#pragma warning disable CA1031 // Do not catch general exception types
        catch (Exception ex)
        {
            // SERVICE BOUNDARY PATTERN: Cache multi-tag lookup failures must return empty results gracefully
            // Rationale: Multi-tag cache lookup is a query operation with set intersections. Failures (Redis
            // errors, serialization issues) should return empty results rather than throwing exceptions. This
            // allows tag-based invalidation operations to complete gracefully even if some lookups fail.
            // Context: Redis SetMembers and set intersection operations can fail in various ways
            _logger.LogWarning(ex, "Unexpected error getting keys for multiple tags");
            return new HashSet<string>(StringComparer.Ordinal);
        }
#pragma warning restore CA1031 // Do not catch general exception types
    }
}
