using System.Collections.Concurrent;

namespace Api.BoundedContexts.KnowledgeBase.Infrastructure.Caching;

/// <summary>
/// ISSUE-3494: Thread-safe LRU (Least Recently Used) cache for L1 in-memory tier.
/// Provides O(1) get/set operations with automatic eviction of least recently used items.
/// </summary>
internal sealed class LruCache<TKey, TValue> : IDisposable where TKey : notnull
{
    private readonly int _maxCapacity;
    private readonly ConcurrentDictionary<TKey, LinkedListNode<CacheEntry>> _cache;
    private readonly LinkedList<CacheEntry> _lruList;
    private readonly ReaderWriterLockSlim _lock;
    private readonly TimeProvider _timeProvider;

    // Metrics
    private long _hits;
    private long _misses;
    private long _evictions;
    private long _totalLatencyMicros; // Store in microseconds for atomic operations
    private long _operationCount;

    /// <summary>
    /// Creates a new LRU cache with specified maximum capacity.
    /// </summary>
    /// <param name="maxCapacity">Maximum number of items to hold (default: 1000)</param>
    /// <param name="timeProvider">Optional time provider for testability</param>
    public LruCache(int maxCapacity = 1000, TimeProvider? timeProvider = null)
    {
        if (maxCapacity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxCapacity), "Capacity must be positive.");
        }

        _maxCapacity = maxCapacity;
        _cache = new ConcurrentDictionary<TKey, LinkedListNode<CacheEntry>>();
        _lruList = new LinkedList<CacheEntry>();
        _lock = new ReaderWriterLockSlim(LockRecursionPolicy.NoRecursion);
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <summary>
    /// Number of items currently in the cache.
    /// </summary>
    public int Count
    {
        get
        {
            _lock.EnterReadLock();
            try
            {
                return _lruList.Count;
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }
    }

    /// <summary>
    /// Maximum capacity of the cache.
    /// </summary>
    public int MaxCapacity => _maxCapacity;

    /// <summary>
    /// Gets cache statistics.
    /// </summary>
    public LruCacheStats GetStats()
    {
        var opCount = Interlocked.Read(ref _operationCount);
        var totalMicros = Interlocked.Read(ref _totalLatencyMicros);

        return new LruCacheStats
        {
            Hits = Interlocked.Read(ref _hits),
            Misses = Interlocked.Read(ref _misses),
            Evictions = Interlocked.Read(ref _evictions),
            EntryCount = Count,
            MaxCapacity = _maxCapacity,
            AverageLatencyMs = opCount > 0
                ? (totalMicros / 1000.0) / opCount
                : 0
        };
    }

    /// <summary>
    /// Tries to get a value from the cache.
    /// On hit, moves the item to the front (most recently used).
    /// </summary>
    /// <param name="key">Cache key</param>
    /// <param name="value">Retrieved value (if found)</param>
    /// <returns>True if found, false if not in cache or expired</returns>
    public bool TryGet(TKey key, out TValue? value)
    {
        var startTime = _timeProvider.GetTimestamp();

        try
        {
            if (!_cache.TryGetValue(key, out var node))
            {
                Interlocked.Increment(ref _misses);
                value = default;
                return false;
            }

            _lock.EnterWriteLock();
            try
            {
                // Check if entry is expired
                if (node.Value.ExpiresAt < _timeProvider.GetUtcNow())
                {
                    // Remove expired entry
                    _lruList.Remove(node);
                    _cache.TryRemove(key, out _);
                    Interlocked.Increment(ref _misses);
                    Interlocked.Increment(ref _evictions);
                    value = default;
                    return false;
                }

                // Move to front (most recently used)
                _lruList.Remove(node);
                _lruList.AddFirst(node);

                // Increment access count for frequency tracking
                node.Value.AccessCount++;

                Interlocked.Increment(ref _hits);
                value = node.Value.Value;
                return true;
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }
        finally
        {
            RecordLatency(startTime);
        }
    }

    /// <summary>
    /// Sets a value in the cache with optional TTL.
    /// If the cache is full, evicts the least recently used item.
    /// </summary>
    /// <param name="key">Cache key</param>
    /// <param name="value">Value to cache</param>
    /// <param name="ttl">Time-to-live (optional, default: no expiration)</param>
    public void Set(TKey key, TValue value, TimeSpan? ttl = null)
    {
        var startTime = _timeProvider.GetTimestamp();

        try
        {
            var expiresAt = ttl.HasValue
                ? _timeProvider.GetUtcNow().Add(ttl.Value)
                : DateTimeOffset.MaxValue;

            _lock.EnterWriteLock();
            try
            {
                // If key exists, update and move to front
                if (_cache.TryGetValue(key, out var existingNode))
                {
                    existingNode.Value.Value = value;
                    existingNode.Value.ExpiresAt = expiresAt;
                    existingNode.Value.AccessCount++;
                    _lruList.Remove(existingNode);
                    _lruList.AddFirst(existingNode);
                    return;
                }

                // Evict if at capacity
                while (_lruList.Count >= _maxCapacity && _lruList.Last != null)
                {
                    var evictNode = _lruList.Last;
                    _lruList.RemoveLast();
                    _cache.TryRemove(evictNode.Value.Key, out _);
                    Interlocked.Increment(ref _evictions);
                }

                // Add new entry at front
                var entry = new CacheEntry
                {
                    Key = key,
                    Value = value,
                    ExpiresAt = expiresAt,
                    CreatedAt = _timeProvider.GetUtcNow(),
                    AccessCount = 1
                };

                var newNode = new LinkedListNode<CacheEntry>(entry);
                _lruList.AddFirst(newNode);
                _cache[key] = newNode;
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }
        finally
        {
            RecordLatency(startTime);
        }
    }

    /// <summary>
    /// Removes a specific key from the cache.
    /// </summary>
    /// <param name="key">Key to remove</param>
    /// <returns>True if removed, false if not found</returns>
    public bool Remove(TKey key)
    {
        _lock.EnterWriteLock();
        try
        {
            if (!_cache.TryRemove(key, out var node))
            {
                return false;
            }

            _lruList.Remove(node);
            return true;
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    /// <summary>
    /// Removes all expired entries from the cache.
    /// Should be called periodically for cleanup.
    /// </summary>
    /// <returns>Number of entries removed</returns>
    public int RemoveExpired()
    {
        var removed = 0;
        var now = _timeProvider.GetUtcNow();

        _lock.EnterWriteLock();
        try
        {
            var node = _lruList.Last;
            while (node != null)
            {
                var prev = node.Previous;
                if (node.Value.ExpiresAt < now)
                {
                    _lruList.Remove(node);
                    _cache.TryRemove(node.Value.Key, out _);
                    removed++;
                    Interlocked.Increment(ref _evictions);
                }
                node = prev;
            }
        }
        finally
        {
            _lock.ExitWriteLock();
        }

        return removed;
    }

    /// <summary>
    /// Clears all entries from the cache.
    /// </summary>
    public void Clear()
    {
        _lock.EnterWriteLock();
        try
        {
            _lruList.Clear();
            _cache.Clear();
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    /// <summary>
    /// Gets the access count for a specific key.
    /// Used for adaptive TTL and promotion decisions.
    /// </summary>
    /// <param name="key">Cache key</param>
    /// <returns>Access count, or 0 if not found</returns>
    public int GetAccessCount(TKey key)
    {
        _lock.EnterReadLock();
        try
        {
            return _cache.TryGetValue(key, out var node) ? node.Value.AccessCount : 0;
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _lock.Dispose();
    }

    private void RecordLatency(long startTimestamp)
    {
        var elapsed = _timeProvider.GetElapsedTime(startTimestamp);
        var micros = (long)(elapsed.TotalMilliseconds * 1000);
        Interlocked.Add(ref _totalLatencyMicros, micros);
        Interlocked.Increment(ref _operationCount);
    }

    /// <summary>
    /// Internal cache entry structure.
    /// </summary>
    private sealed class CacheEntry
    {
        public required TKey Key { get; init; }
        public required TValue Value { get; set; }
        public required DateTimeOffset ExpiresAt { get; set; }
        public required DateTimeOffset CreatedAt { get; init; }
        public int AccessCount { get; set; }
    }
}

/// <summary>
/// Statistics for LRU cache monitoring.
/// </summary>
internal class LruCacheStats
{
    /// <summary>
    /// Number of cache hits.
    /// </summary>
    public long Hits { get; init; }

    /// <summary>
    /// Number of cache misses.
    /// </summary>
    public long Misses { get; init; }

    /// <summary>
    /// Number of evictions (due to capacity or expiration).
    /// </summary>
    public long Evictions { get; init; }

    /// <summary>
    /// Current number of entries.
    /// </summary>
    public int EntryCount { get; init; }

    /// <summary>
    /// Maximum capacity.
    /// </summary>
    public int MaxCapacity { get; init; }

    /// <summary>
    /// Hit rate percentage (0-100).
    /// </summary>
    public double HitRatePercent
    {
        get
        {
            var total = Hits + Misses;
            return total > 0 ? (Hits * 100.0) / total : 0;
        }
    }

    /// <summary>
    /// Average operation latency in milliseconds.
    /// </summary>
    public double AverageLatencyMs { get; init; }
}
