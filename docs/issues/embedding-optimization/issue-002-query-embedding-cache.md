# Issue #2: Query Embedding Cache

**Priority**: 🔴 Critical (Quick Win)
**Category**: Performance Optimization
**Effort**: 6-8 hours
**Impact**: High - 60% latency reduction

---

## 📋 Problem

Every user query generates a new embedding, even for frequently asked questions:
```csharp
// Current: NO caching
var embedding = await _embeddingService.GenerateEmbeddingAsync(query);
// Every call hits Ollama/OpenRouter API ❌
```

**Impact**:
- **Query latency**: 150-200ms per embedding
- **API costs**: 100K queries/month × $0.000002 = $0.20/mese (OpenAI)
- **Server load**: Unnecessary CPU/network usage
- **User experience**: Slower search responses

---

## 🎯 Solution

Implement **memory-based embedding cache** with normalized query keys:

```csharp
// Proposed: WITH caching
public class CachedEmbeddingService : IEmbeddingService
{
    private readonly IEmbeddingService _inner;
    private readonly IMemoryCache _cache;

    public async Task<EmbeddingResult> GenerateEmbeddingAsync(string text, CancellationToken ct)
    {
        // Normalize query (lowercase, trim)
        var cacheKey = $"emb:{NormalizeText(text)}";

        // Cache hit?
        if (_cache.TryGetValue<EmbeddingResult>(cacheKey, out var cached))
        {
            _logger.LogDebug("Embedding cache HIT for: {Text}", text[..Math.Min(50, text.Length)]);
            return cached!;
        }

        // Cache miss → generate
        var result = await _inner.GenerateEmbeddingAsync(text, ct);

        if (result.Success)
        {
            var cacheOptions = new MemoryCacheEntryOptions()
                .SetSize(1) // 1 cache entry
                .SetSlidingExpiration(TimeSpan.FromHours(24))
                .SetAbsoluteExpiration(TimeSpan.FromDays(7));

            _cache.Set(cacheKey, result, cacheOptions);
        }

        return result;
    }

    private string NormalizeText(string text)
    {
        // Lowercase, trim, collapse whitespace
        return Regex.Replace(text.ToLowerInvariant().Trim(), @"\s+", " ");
    }
}
```

---

## 💰 Benefits

| Metric | Before | After | Improvement |
|--------|--------|-------|-------------|
| **Query Latency P95** | 200ms | 80ms (cache) | **-60%** |
| **API Calls (OpenAI)** | 100K/month | 40K/month | **-60%** |
| **Monthly Cost** | $0.20 | $0.08 | **-$0.12** |
| **Server CPU** | 100% | 40% | **-60%** |

---

## 🛠️ Implementation Tasks

### 1. Create CachedEmbeddingService
```csharp
// File: Services/CachedEmbeddingService.cs
public class CachedEmbeddingService : IEmbeddingService
{
    private readonly IEmbeddingService _inner;
    private readonly IMemoryCache _cache;
    private readonly ILogger<CachedEmbeddingService> _logger;

    // Constructor injection...

    public async Task<EmbeddingResult> GenerateEmbeddingAsync(string text, CancellationToken ct = default)
    {
        var cacheKey = ComputeCacheKey(text);

        if (_cache.TryGetValue<EmbeddingResult>(cacheKey, out var cached))
        {
            _logger.LogDebug("Embedding cache HIT");
            return cached!;
        }

        _logger.LogDebug("Embedding cache MISS");
        var result = await _inner.GenerateEmbeddingAsync(text, ct);

        if (result.Success)
        {
            CacheResult(cacheKey, result);
        }

        return result;
    }

    private string ComputeCacheKey(string text)
    {
        var normalized = Regex.Replace(text.ToLowerInvariant().Trim(), @"\s+", " ");
        var hash = ComputeSha256(normalized);
        return $"emb:{hash[..16]}"; // Use hash prefix as key
    }

    private void CacheResult(string key, EmbeddingResult result)
    {
        var options = new MemoryCacheEntryOptions()
            .SetSize(1)
            .SetSlidingExpiration(TimeSpan.FromHours(24))
            .SetAbsoluteExpiration(TimeSpan.FromDays(7));

        _cache.Set(key, result, options);
    }
}
```

### 2. Register Decorator in DI
```csharp
// File: Program.cs or ApplicationServiceExtensions.cs

services.AddMemoryCache(options =>
{
    options.SizeLimit = 10000; // Max 10K cached embeddings
    options.CompactionPercentage = 0.2; // Compact when 80% full
});

// Register base service
services.AddScoped<EmbeddingService>();

// Decorate with cache
services.Decorate<IEmbeddingService, CachedEmbeddingService>();
```

### 3. Add Cache Metrics
```csharp
// File: Observability/EmbeddingCacheMetrics.cs
public static class EmbeddingCacheMetrics
{
    private static readonly Counter CacheHits = Metrics.CreateCounter(
        "embedding_cache_hits_total",
        "Total embedding cache hits");

    private static readonly Counter CacheMisses = Metrics.CreateCounter(
        "embedding_cache_misses_total",
        "Total embedding cache misses");

    public static void RecordHit() => CacheHits.Inc();
    public static void RecordMiss() => CacheMisses.Inc();
}
```

### 4. Grafana Dashboard Query
```promql
# Cache hit rate
rate(embedding_cache_hits_total[5m]) /
(rate(embedding_cache_hits_total[5m]) + rate(embedding_cache_misses_total[5m]))

# Target: >60% hit rate
```

---

## ✅ Acceptance Criteria

1. **Cache Hit Rate**
   - [ ] >60% cache hit rate after 1 hour of normal traffic
   - [ ] >75% cache hit rate after 24 hours

2. **Performance**
   - [ ] Cache hit latency <50ms (vs 200ms cold)
   - [ ] P95 query latency reduced by >50%

3. **Memory Usage**
   - [ ] Cache size limited to 10K entries
   - [ ] Memory usage <100MB for cache
   - [ ] Automatic eviction when size limit reached

4. **Metrics**
   - [ ] Prometheus metrics exported
   - [ ] Grafana dashboard shows hit/miss rate
   - [ ] Cache size monitored

5. **Correctness**
   - [ ] Identical embeddings for identical queries
   - [ ] Case-insensitive matching ("Catan" = "catan")
   - [ ] Whitespace normalization ("  foo  bar  " = "foo bar")

---

## 🧪 Testing

```csharp
[Fact]
public async Task CacheHit_ReturnsCachedEmbedding()
{
    // Arrange
    var mockInner = new Mock<IEmbeddingService>();
    var cache = new MemoryCache(new MemoryCacheOptions());
    var service = new CachedEmbeddingService(mockInner.Object, cache, logger);

    var expected = EmbeddingResult.CreateSuccess(new List<float[]> { new float[768] });
    mockInner.Setup(s => s.GenerateEmbeddingAsync("test", default))
        .ReturnsAsync(expected);

    // Act
    var result1 = await service.GenerateEmbeddingAsync("test");
    var result2 = await service.GenerateEmbeddingAsync("test"); // Should hit cache

    // Assert
    mockInner.Verify(s => s.GenerateEmbeddingAsync("test", default), Times.Once); // Only called once
    Assert.Same(result1, result2); // Same instance from cache
}

[Fact]
public async Task NormalizedQueriesShareCache()
{
    // Act
    var result1 = await service.GenerateEmbeddingAsync("Come si vince a Catan?");
    var result2 = await service.GenerateEmbeddingAsync("  come si VINCE a catan?  ");

    // Assert
    mockInner.Verify(s => s.GenerateEmbeddingAsync(It.IsAny<string>(), default), Times.Once);
}
```

---

## 📈 Success Metrics

- **Cache Hit Rate**: >60% (target: 75%)
- **Latency Reduction**: >50% for cached queries
- **Cost Savings**: $0.12/mese (with OpenAI)
- **Memory Overhead**: <100MB

---

**Created**: 2025-11-22
**Estimated Effort**: 6-8 hours
**Priority**: 🔴 Critical
