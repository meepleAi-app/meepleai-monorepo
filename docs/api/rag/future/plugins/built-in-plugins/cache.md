# Cache Plugins

> **Result Caching for Performance Optimization**

Cache plugins store and retrieve previous results to avoid redundant processing. They're especially valuable for expensive retrieval and generation operations.

## Semantic Cache

**Plugin ID**: `cache-semantic-v1`

Caches results by semantic similarity, returning cached responses for similar (not just identical) queries.

### Configuration

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `similarityThreshold` | number | `0.95` | Minimum similarity for cache hit |
| `maxCacheAge` | integer | `3600` | Cache TTL in seconds |
| `namespace` | string | `"default"` | Cache partition |
| `embeddingModel` | string | `text-embedding-3-small` | Model for query embedding |
| `maxEntries` | integer | `10000` | Maximum cached entries |
| `evictionPolicy` | string | `"lru"` | Eviction: `"lru"`, `"ttl"`, `"fifo"` |

### Input Schema

```json
{
  "type": "object",
  "properties": {
    "query": { "type": "string" },
    "gameId": { "type": "string", "format": "uuid" },
    "forceRefresh": { "type": "boolean", "default": false }
  },
  "required": ["query"]
}
```

### Output Schema

```json
{
  "type": "object",
  "properties": {
    "cacheHit": { "type": "boolean" },
    "cachedResult": { "type": "object" },
    "cachedAt": { "type": "string", "format": "date-time" },
    "similarity": { "type": "number" },
    "originalQuery": { "type": "string" }
  }
}
```

### Usage Example

```json
{
  "id": "semantic-cache",
  "pluginId": "cache-semantic-v1",
  "config": {
    "similarityThreshold": 0.93,
    "maxCacheAge": 7200,
    "namespace": "game-rules"
  }
}
```

### Pipeline Pattern

```
[Semantic Cache] ──┬─→ (cache hit) ─────────────→ [Exit]
                   └─→ (cache miss) → [Retrieval] → [Generation] → [Store Result]
```

### Conditional Edge for Cache Hit

```json
{
  "id": "cache-hit",
  "source": "semantic-cache",
  "target": "exit",
  "condition": "output.result.cacheHit === true",
  "label": "Cache Hit"
}
```

---

## Exact Match Cache

**Plugin ID**: `cache-exact-v1`

Simple hash-based cache for exact query matches. Faster but less flexible.

### Configuration

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `maxCacheAge` | integer | `3600` | Cache TTL in seconds |
| `namespace` | string | `"default"` | Cache partition |
| `includeGameId` | boolean | `true` | Include gameId in cache key |
| `includeUserId` | boolean | `false` | Include userId in cache key |
| `caseSensitive` | boolean | `false` | Case-sensitive matching |
| `normalizeWhitespace` | boolean | `true` | Normalize query whitespace |

### Output Schema

```json
{
  "type": "object",
  "properties": {
    "cacheHit": { "type": "boolean" },
    "cachedResult": { "type": "object" },
    "cachedAt": { "type": "string", "format": "date-time" },
    "cacheKey": { "type": "string" }
  }
}
```

### Usage Example

```json
{
  "id": "exact-cache",
  "pluginId": "cache-exact-v1",
  "config": {
    "maxCacheAge": 1800,
    "includeGameId": true,
    "caseSensitive": false
  }
}
```

---

## Comparison

| Feature | Semantic Cache | Exact Match Cache |
|---------|---------------|-------------------|
| Similar queries | ✅ Matched | ❌ Not matched |
| Latency | ~50ms | ~5ms |
| Storage | Higher (vectors) | Lower (hashes) |
| False positives | Possible | None |
| Best for | Natural language | Repeated exact queries |

## Cache Strategy

### When to Use Caching

✅ **Good candidates**:
- Frequently asked questions
- Expensive generation operations
- Stable knowledge base content
- High-traffic queries

❌ **Avoid caching**:
- Time-sensitive information
- Personalized responses
- Rapidly changing data
- Low-repeat query patterns

### Cache Positioning

```
Pipeline Entry → [Cache Check]
                     │
                     ├─ Hit: Return cached result
                     │
                     └─ Miss: Continue pipeline
                              │
                         [Processing...]
                              │
                         [Cache Store] ← Store before exit
```

### Cache Invalidation

```csharp
// Manual invalidation via API
await cachePlugin.InvalidateAsync(
    namespace: "game-rules",
    gameId: gameId
);

// Time-based invalidation (automatic)
config.MaxCacheAge = 3600; // 1 hour
```

## Best Practices

### Threshold Tuning

| Threshold | Behavior |
|-----------|----------|
| 0.99+ | Very strict, almost exact match |
| 0.95 | Recommended default |
| 0.90 | More aggressive caching |
| < 0.90 | Risk of incorrect cache hits |

### Monitoring

Track these metrics:
- **Hit rate**: Target > 30% for frequently asked content
- **False positive rate**: Should be < 1%
- **Cache size**: Monitor growth and eviction
- **Latency savings**: Measure actual improvement

### Testing

```csharp
[Fact]
public async Task SemanticCache_ReturnsCachedResult_ForSimilarQuery()
{
    // First query
    var input1 = PluginMocks.CreateQueryInput("How do I setup Catan?");
    var output1 = await Plugin.ExecuteAsync(input1);
    output1.Result!.RootElement.GetProperty("cacheHit").GetBoolean().Should().BeFalse();

    // Similar query should hit cache
    var input2 = PluginMocks.CreateQueryInput("How to set up Catan?");
    var output2 = await Plugin.ExecuteAsync(input2);
    output2.Result!.RootElement.GetProperty("cacheHit").GetBoolean().Should().BeTrue();
}

[Fact]
public async Task SemanticCache_MissesCache_ForDifferentQuery()
{
    var input1 = PluginMocks.CreateQueryInput("How do I setup Catan?");
    await Plugin.ExecuteAsync(input1);

    var input2 = PluginMocks.CreateQueryInput("What's the best strategy for Catan?");
    var output2 = await Plugin.ExecuteAsync(input2);
    output2.Result!.RootElement.GetProperty("cacheHit").GetBoolean().Should().BeFalse();
}
```
