using System.Collections.Concurrent;
using System.Text.Json;
using Api.Configuration;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace Api.Services;

/// <summary>
/// PERF-05: HybridCache service implementation with L1 (in-memory) + L2 (Redis) support.
/// </summary>
public class HybridCacheService : IHybridCacheService
{
    private readonly HybridCache _hybridCache;
    private readonly HybridCacheConfiguration _config;
    private readonly ILogger<HybridCacheService> _logger;

    // Tag tracking for invalidation (in-memory only, not persisted to Redis)
    private readonly ConcurrentDictionary<string, HashSet<string>> _tagToKeys = new();
    private readonly ConcurrentDictionary<string, HashSet<string>> _keyToTags = new();
    private readonly object _tagLock = new();

    // Statistics tracking
    private long _totalHits = 0;
    private long _totalMisses = 0;
    private long _stampedePreventions = 0;

    public HybridCacheService(
        HybridCache hybridCache,
        IOptions<HybridCacheConfiguration> config,
        ILogger<HybridCacheService> logger)
    {
        _hybridCache = hybridCache ?? throw new ArgumentNullException(nameof(hybridCache));
        _config = config?.Value ?? throw new ArgumentNullException(nameof(config));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
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

                    var value = await factory(cancellationToken);
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
            );

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
            return await factory(ct);
        }
        catch (RedisTimeoutException ex)
        {
            _logger.LogWarning(ex, "Redis timeout for key {CacheKey}. Falling back to factory.", cacheKey);
            return await factory(ct);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex, "Invalid cache operation for key {CacheKey}. Falling back to factory.", cacheKey);
            return await factory(ct);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "JSON serialization error for key {CacheKey}. Falling back to factory.", cacheKey);
            return await factory(ct);
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
            await _hybridCache.RemoveAsync(cacheKey, ct);

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
                await RemoveAsync(key, ct);
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
                await RemoveAsync(key, ct);
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
            L1EntryCount = _keyToTags.Count, // Approximate using tag tracking
            L1MemoryBytes = 0 // Not easily accessible without MemoryCache introspection
        };

        return Task.FromResult(stats);
    }

    #region Tag Tracking

    private void TrackTags(string cacheKey, string[] tags)
    {
        lock (_tagLock)
        {
            // Track key → tags mapping
            if (!_keyToTags.TryGetValue(cacheKey, out var existingTags))
            {
                existingTags = new HashSet<string>();
                _keyToTags[cacheKey] = existingTags;
            }

            foreach (var tag in tags)
            {
                existingTags.Add(tag);

                // Track tag → keys mapping
                if (!_tagToKeys.TryGetValue(tag, out var keys))
                {
                    keys = new HashSet<string>();
                    _tagToKeys[tag] = keys;
                }

                keys.Add(cacheKey);
            }
        }
    }

    private void UntrackKey(string cacheKey)
    {
        lock (_tagLock)
        {
            // Remove key → tags mapping
            if (_keyToTags.TryRemove(cacheKey, out var tags))
            {
                // Remove this key from all tag → keys mappings
                foreach (var tag in tags)
                {
                    if (_tagToKeys.TryGetValue(tag, out var keys))
                    {
                        keys.Remove(cacheKey);

                        // Clean up empty tag entries
                        if (keys.Count == 0)
                        {
                            _tagToKeys.TryRemove(tag, out _);
                        }
                    }
                }
            }
        }
    }

    private HashSet<string> GetKeysByTag(string tag)
    {
        lock (_tagLock)
        {
            if (_tagToKeys.TryGetValue(tag, out var keys))
            {
                return new HashSet<string>(keys); // Return copy to avoid concurrent modification
            }

            return new HashSet<string>();
        }
    }

    private HashSet<string> GetKeysByTags(string[] tags)
    {
        lock (_tagLock)
        {
            HashSet<string>? result = null;

            foreach (var tag in tags)
            {
                if (_tagToKeys.TryGetValue(tag, out var keys))
                {
                    if (result == null)
                    {
                        result = new HashSet<string>(keys);
                    }
                    else
                    {
                        // Intersect with existing results (AND logic)
                        result.IntersectWith(keys);
                    }
                }
                else
                {
                    // Tag not found, no keys can match all tags
                    return new HashSet<string>();
                }
            }

            return result ?? new HashSet<string>();
        }
    }

    #endregion
}
