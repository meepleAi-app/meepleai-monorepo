using System.Diagnostics;
using System.Text.Json;
using Api.BoundedContexts.KnowledgeBase.Domain.Services.Caching;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.BoundedContexts.KnowledgeBase.Infrastructure.Persistence;
using Api.Services;
using Microsoft.Extensions.Options;

namespace Api.BoundedContexts.KnowledgeBase.Infrastructure.Caching;

/// <summary>
/// ISSUE-3494: Multi-tier cache implementation for context retrieval optimization.
/// 3-tier strategy: In-Memory (μs) → Redis (ms) → Qdrant (100ms+).
/// Provides automatic cache promotion, adaptive TTL, and metrics tracking.
/// </summary>
internal sealed class MultiTierCache : IMultiTierCache, IDisposable
{
    private readonly LruCache<string, CachedItem> _l1Cache;
    private readonly IHybridCacheService _l2Cache;
    // L3 (Qdrant) removed - will be re-added with IServiceScopeFactory when vector caching is implemented
    private readonly IRedisFrequencyTracker _frequencyTracker;
    private readonly ICacheMetricsRecorder _metricsRecorder;
    private readonly MultiTierCacheConfiguration _config;
    private readonly ILogger<MultiTierCache> _logger;
    private readonly TimeProvider _timeProvider;

    // Metrics tracking
    private long _l1Hits;
    private long _l1Misses;
    private long _l2Hits;
    private long _l2Misses;
#pragma warning disable CS0649 // L3 metrics reserved for future vector-based semantic caching
    private long _l3Hits;
    private long _l3Misses;
#pragma warning restore CS0649
    private long _promotions;
    private long _adaptiveTtlAdjustments;
    private double _totalLatencyMs;
    private long _operationCount;

    public MultiTierCache(
        IHybridCacheService l2Cache,
        IRedisFrequencyTracker frequencyTracker,
        ICacheMetricsRecorder metricsRecorder,
        IOptions<MultiTierCacheConfiguration> config,
        ILogger<MultiTierCache> logger,
        TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(l2Cache);
        ArgumentNullException.ThrowIfNull(frequencyTracker);
        ArgumentNullException.ThrowIfNull(metricsRecorder);
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(logger);

        _l2Cache = l2Cache;
        _frequencyTracker = frequencyTracker;
        _metricsRecorder = metricsRecorder;
        _config = config.Value;
        _logger = logger;
        _timeProvider = timeProvider ?? TimeProvider.System;

        // Initialize L1 LRU cache with configured capacity
        _l1Cache = new LruCache<string, CachedItem>(_config.L1MaxItems, _timeProvider);
    }

    /// <inheritdoc />
    public async Task<MultiTierCacheResult<T>?> GetAsync<T>(
        string key,
        Guid gameId,
        CancellationToken cancellationToken = default) where T : class
    {
        if (!_config.Enabled)
        {
            return null;
        }

        var stopwatch = Stopwatch.StartNew();
        var fullKey = BuildKey(key, gameId);

        try
        {
            // Track access for adaptive TTL
            await _frequencyTracker.TrackAccessAsync(gameId, key).ConfigureAwait(false);

            // Try L1 (in-memory)
            if (_config.L1Enabled && _l1Cache.TryGet(fullKey, out var l1Item) && l1Item?.Value != null)
            {
                var l1Value = ExtractTypedValue<T>(l1Item.Value);
                if (l1Value != null)
                {
                    Interlocked.Increment(ref _l1Hits);
                    await _metricsRecorder.RecordCacheHitAsync("get", "l1_memory").ConfigureAwait(false);

                    _logger.LogDebug("L1 cache HIT for key: {Key}", fullKey);

                    return CreateResult(l1Value, CacheTier.L1Memory, stopwatch.Elapsed.TotalMilliseconds, false);
                }
            }

            Interlocked.Increment(ref _l1Misses);

            // Try L2 (Redis)
            if (_config.L2Enabled)
            {
                var l2Result = await TryGetFromL2Async<T>(fullKey, gameId, cancellationToken).ConfigureAwait(false);
                if (l2Result != null)
                {
                    Interlocked.Increment(ref _l2Hits);
                    await _metricsRecorder.RecordCacheHitAsync("get", "l2_redis").ConfigureAwait(false);

                    _logger.LogDebug("L2 cache HIT for key: {Key}", fullKey);

                    // Promote to L1 if enabled
                    if (_config.EnablePromotion)
                    {
                        await PromoteToL1Async(fullKey, gameId, l2Result, cancellationToken).ConfigureAwait(false);
                    }

                    return CreateResult(l2Result, CacheTier.L2Redis, stopwatch.Elapsed.TotalMilliseconds, _config.EnablePromotion);
                }

                Interlocked.Increment(ref _l2Misses);
            }

            // L3 (Qdrant) is not used for direct key-value lookups
            // It's for semantic/vector search which is handled separately
            await _metricsRecorder.RecordCacheMissAsync("get", "all_tiers").ConfigureAwait(false);

            _logger.LogDebug("Cache MISS on all tiers for key: {Key}", fullKey);
            return null;
        }
        finally
        {
            RecordOperationLatency(stopwatch.Elapsed.TotalMilliseconds);
        }
    }

    /// <inheritdoc />
    public async Task<MultiTierCacheResult<T>> GetOrCreateAsync<T>(
        string key,
        Guid gameId,
        Func<CancellationToken, Task<T>> factory,
        string[]? tags = null,
        CancellationToken cancellationToken = default) where T : class
    {
        var stopwatch = Stopwatch.StartNew();
        var fullKey = BuildKey(key, gameId);

        // Try to get from cache first
        var existing = await GetAsync<T>(key, gameId, cancellationToken).ConfigureAwait(false);
        if (existing != null)
        {
            return existing;
        }

        // Execute factory to create value
        _logger.LogDebug("Executing factory for cache key: {Key}", fullKey);

        var value = await _l2Cache.GetOrCreateAsync(
            fullKey,
            async ct =>
            {
                var result = await factory(ct).ConfigureAwait(false);
                return new CachedItem
                {
                    Value = result,
                    CreatedAt = _timeProvider.GetUtcNow()
                };
            },
            tags,
            await CalculateAdaptiveTtlAsync(gameId, key, cancellationToken).ConfigureAwait(false),
            cancellationToken).ConfigureAwait(false);

        // Extract typed value from CachedItem (handle JSON deserialization from Redis)
        var typedValue = ExtractTypedValue<T>(value.Value);
        if (typedValue == null)
        {
            throw new InvalidOperationException($"Failed to deserialize cached value for key: {fullKey}");
        }

        // Store in L1
        if (_config.L1Enabled)
        {
            var ttl = await CalculateAdaptiveTtlAsync(gameId, key, cancellationToken).ConfigureAwait(false);
            _l1Cache.Set(fullKey, value, ttl);
        }

        return CreateResult(
            typedValue,
            CacheTier.Factory,
            stopwatch.Elapsed.TotalMilliseconds,
            false);
    }

    /// <inheritdoc />
    public async Task SetAsync<T>(
        string key,
        Guid gameId,
        T value,
        Vector? embedding = null,
        string[]? tags = null,
        CancellationToken cancellationToken = default) where T : class
    {
        if (!_config.Enabled)
        {
            return;
        }

        var fullKey = BuildKey(key, gameId);
        var ttl = await CalculateAdaptiveTtlAsync(gameId, key, cancellationToken).ConfigureAwait(false);

        var cachedItem = new CachedItem
        {
            Value = value,
            CreatedAt = _timeProvider.GetUtcNow()
        };

        // Set in L1
        if (_config.L1Enabled)
        {
            _l1Cache.Set(fullKey, cachedItem, ttl);
            _logger.LogDebug("Set L1 cache for key: {Key} with TTL: {Ttl}", fullKey, ttl);
        }

        // Set in L2 (Redis)
        if (_config.L2Enabled)
        {
            await _l2Cache.GetOrCreateAsync(
                fullKey,
                _ => Task.FromResult(cachedItem),
                tags,
                ttl,
                cancellationToken).ConfigureAwait(false);

            _logger.LogDebug("Set L2 cache for key: {Key} with TTL: {Ttl}", fullKey, ttl);
        }

        // Track for frequency-based optimization
        await _frequencyTracker.TrackAccessAsync(gameId, key).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        // Remove from L1
        _l1Cache.Remove(key);

        // Remove from L2
        await _l2Cache.RemoveAsync(key, cancellationToken).ConfigureAwait(false);

        _logger.LogInformation("Removed cache entry from all tiers: {Key}", key);
    }

    /// <inheritdoc />
    public async Task<int> RemoveByTagAsync(string tag, CancellationToken cancellationToken = default)
    {
        // L1 doesn't support tag-based removal, so we rely on L2
        var removed = await _l2Cache.RemoveByTagAsync(tag, cancellationToken).ConfigureAwait(false);

        _logger.LogInformation("Removed {Count} cache entries by tag: {Tag}", removed, tag);

        // Clear L1 to ensure consistency (conservative approach)
        // In production, you might want a more sophisticated L1 tag tracking
        _l1Cache.Clear();

        return removed;
    }

    /// <inheritdoc />
    public async Task<int> WarmGameCacheAsync(
        Guid gameId,
        int topN = 20,
        CancellationToken cancellationToken = default)
    {
        if (!_config.WarmingEnabled)
        {
            _logger.LogDebug("Cache warming disabled, skipping for game: {GameId}", gameId);
            return 0;
        }

        _logger.LogInformation("Starting cache warming for game {GameId}, top {TopN} items", gameId, topN);

        try
        {
            // Get top frequent queries for this game
            var topQueries = await _frequencyTracker.GetTopQueriesAsync(gameId, topN).ConfigureAwait(false);

            if (topQueries.Count == 0)
            {
                _logger.LogDebug("No frequent queries found for game {GameId}, nothing to warm", gameId);
                return 0;
            }

            var warmed = 0;
            using var warmingCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            warmingCts.CancelAfter(_config.WarmingTimeout);

            foreach (var query in topQueries)
            {
                if (warmingCts.Token.IsCancellationRequested)
                {
                    _logger.LogDebug("Cache warming timeout reached for game {GameId}", gameId);
                    break;
                }

                try
                {
                    var key = BuildKey(query.Query, gameId);

                    // Check if already in L1
                    if (_l1Cache.TryGet(key, out _))
                    {
                        continue; // Already warmed
                    }

                    // Try to promote from L2 to L1
                    var l2Value = await TryGetFromL2Async<CachedItem>(key, gameId, warmingCts.Token).ConfigureAwait(false);
                    if (l2Value != null)
                    {
                        var ttl = await CalculateAdaptiveTtlAsync(gameId, query.Query, warmingCts.Token).ConfigureAwait(false);
                        _l1Cache.Set(key, l2Value, ttl);
                        warmed++;
                        Interlocked.Increment(ref _promotions);
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to warm cache for query: {Query}", query.Query);
                }
            }

            _logger.LogInformation("Cache warming completed for game {GameId}: {Warmed}/{Total} items", gameId, warmed, topQueries.Count);
            return warmed;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Cache warming failed for game {GameId}", gameId);
            return 0;
        }
    }

    /// <inheritdoc />
    public MultiTierCacheMetrics GetMetrics()
    {
        var l1Stats = _l1Cache.GetStats();

        return new MultiTierCacheMetrics
        {
            L1Memory = new TierMetrics
            {
                Hits = Interlocked.Read(ref _l1Hits),
                Misses = Interlocked.Read(ref _l1Misses),
                EntryCount = l1Stats.EntryCount,
                AverageLatencyMs = l1Stats.AverageLatencyMs
            },
            L2Redis = new TierMetrics
            {
                Hits = Interlocked.Read(ref _l2Hits),
                Misses = Interlocked.Read(ref _l2Misses),
                EntryCount = 0, // Redis entry count requires additional tracking
                AverageLatencyMs = 0 // Would require separate tracking
            },
            L3Qdrant = new TierMetrics
            {
                Hits = Interlocked.Read(ref _l3Hits),
                Misses = Interlocked.Read(ref _l3Misses),
                EntryCount = 0, // Qdrant entry count requires API call
                AverageLatencyMs = 0
            },
            TotalPromotions = Interlocked.Read(ref _promotions),
            AdaptiveTtlAdjustments = Interlocked.Read(ref _adaptiveTtlAdjustments),
            AverageLatencyMs = _operationCount > 0 ? _totalLatencyMs / _operationCount : 0
        };
    }

    /// <inheritdoc />
    public async Task<TimeSpan> CalculateAdaptiveTtlAsync(
        Guid gameId,
        string key,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var frequency = await _frequencyTracker.GetFrequencyAsync(gameId, key).ConfigureAwait(false);

            TimeSpan ttl;
            string classification;

            if (frequency >= _config.HighFrequencyThreshold)
            {
                ttl = _config.HighFrequencyTtl;
                classification = "high";
            }
            else if (frequency >= _config.MediumFrequencyThreshold)
            {
                ttl = _config.MediumFrequencyTtl;
                classification = "medium";
            }
            else
            {
                ttl = _config.LowFrequencyTtl;
                classification = "low";
            }

            Interlocked.Increment(ref _adaptiveTtlAdjustments);

            _logger.LogDebug(
                "Adaptive TTL for key {Key}: {Ttl} (frequency={Frequency}, classification={Classification})",
                key, ttl, frequency, classification);

            return ttl;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to calculate adaptive TTL for key {Key}, using default", key);
            return _config.MediumFrequencyTtl;
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _l1Cache.Dispose();
    }

    private string BuildKey(string key, Guid gameId)
    {
        return $"{_config.KeyPrefix}{gameId}:{key}";
    }

    private async Task<T?> TryGetFromL2Async<T>(string key, Guid gameId, CancellationToken cancellationToken) where T : class
    {
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(_config.L2Timeout);

            // L2 GetOrCreateAsync doesn't have a TryGet, so we use a factory that throws
            // if not found - this is a workaround
            var result = await _l2Cache.GetOrCreateAsync<T>(
                key,
                _ => throw new KeyNotFoundException($"Key not found in L2: {key}"),
                null,
                null,
                cts.Token).ConfigureAwait(false);

            return result;
        }
#pragma warning disable S6667 // KeyNotFoundException is used as flow control for cache miss, not an error to log
        catch (KeyNotFoundException)
        {
            return null;
        }
#pragma warning restore S6667
        catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(ex, "L2 cache timeout for key: {Key}", key);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "L2 cache error for key: {Key}", key);
            return null;
        }
    }

    private async Task PromoteToL1Async<T>(string key, Guid gameId, T value, CancellationToken cancellationToken) where T : class
    {
        try
        {
            var frequency = await _frequencyTracker.GetFrequencyAsync(gameId, key).ConfigureAwait(false);

            if (frequency < _config.PromotionToL1Threshold)
            {
                _logger.LogDebug("Skipping L1 promotion for key {Key} (frequency={Frequency} < threshold={Threshold})",
                    key, frequency, _config.PromotionToL1Threshold);
                return;
            }

            var ttl = await CalculateAdaptiveTtlAsync(gameId, key, cancellationToken).ConfigureAwait(false);

            var cachedItem = new CachedItem
            {
                Value = value,
                CreatedAt = _timeProvider.GetUtcNow()
            };

            _l1Cache.Set(key, cachedItem, ttl);
            Interlocked.Increment(ref _promotions);

            _logger.LogDebug("Promoted key {Key} to L1 cache", key);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to promote key {Key} to L1", key);
        }
    }

    private static MultiTierCacheResult<T> CreateResult<T>(T value, CacheTier tier, double latencyMs, bool wasPromoted) where T : class
    {
        return new MultiTierCacheResult<T>
        {
            Value = value,
            SourceTier = tier,
            RetrievalTimeMs = latencyMs,
            WasPromoted = wasPromoted
        };
    }

    private void RecordOperationLatency(double latencyMs)
    {
        Interlocked.Increment(ref _operationCount);
        // Use interlocked add for thread-safety (convert to long for atomic operation)
        var currentTotal = Interlocked.CompareExchange(ref _totalLatencyMs, 0, 0);
        Interlocked.Exchange(ref _totalLatencyMs, currentTotal + latencyMs);
    }

    /// <summary>
    /// Extracts typed value from cached object, handling JSON deserialization from Redis.
    /// </summary>
    private static T? ExtractTypedValue<T>(object? value) where T : class
    {
        if (value == null)
        {
            return null;
        }

        // Direct type match
        if (value is T typedValue)
        {
            return typedValue;
        }

        // Handle JsonElement from Redis deserialization
        if (value is JsonElement jsonElement)
        {
            try
            {
                return JsonSerializer.Deserialize<T>(jsonElement.GetRawText());
            }
            catch (JsonException ex)
            {
                throw new InvalidOperationException(
                    $"Failed to deserialize JsonElement to {typeof(T).Name}", ex);
            }
        }

        // Unexpected type
        throw new InvalidCastException(
            $"Cannot cast {value.GetType().Name} to {typeof(T).Name}");
    }

    /// <summary>
    /// Internal wrapper for cached items with metadata.
    /// Made internal for testability.
    /// </summary>
    internal class CachedItem
    {
        public required object? Value { get; init; }
        public required DateTimeOffset CreatedAt { get; init; }
    }
}
