# AI-10 Cache Optimization - System Architecture

**Status**: Design Complete
**Version**: 1.0
**Last Updated**: 2025-10-19

## Overview

This document details the system architecture for AI-10 cache optimization, implementing metrics integration, dynamic TTL calculation, cache warming, and enhanced invalidation for MeepleAI's Redis-based response cache.

## Architecture Decision

**Selected Approach**: Layered Architecture with Strategy Pattern (Option B)

**Rationale**: Balances clean separation of concerns with pragmatic complexity, aligns with existing MeepleAI service patterns, and enables future extensibility without over-engineering.

---

## Component Architecture

### High-Level Component Diagram

```
┌──────────────────────────────────────────────────────────────────────┐
│ API Consumers (StreamingQaService, RagService, SetupGuideService)   │
└──────────────────────────────────────────────────────────────────────┘
                               │
                               ▼
┌──────────────────────────────────────────────────────────────────────┐
│ AiResponseCacheService (Orchestrator)                                │
│  - GetAsync() → metrics + frequency tracking                         │
│  - SetAsync() → dynamic TTL calculation + metrics                    │
│  - InvalidateAsync() → pattern matching + metrics                    │
│  - InvalidateByGameIdAsync() → batch invalidation                    │
└──────────────────────────────────────────────────────────────────────┘
         │                    │                    │
         ▼                    ▼                    ▼
┌───────────────────┐ ┌──────────────────┐ ┌─────────────────────┐
│ ICacheMetrics     │ │ ITtlCalculation  │ │ IFrequencyTracker   │
│ Recorder          │ │ Strategy         │ │                     │
│                   │ │                  │ │                     │
│ - RecordHit()     │ │ - CalculateTtl() │ │ - IncrementAsync()  │
│ - RecordMiss()    │ │                  │ │ - GetTopKeys()      │
│ - RecordInval()   │ │                  │ │ - GetFrequency()    │
└───────────────────┘ └──────────────────┘ └─────────────────────┘
         │                    │                    │
         ▼                    ▼                    ▼
┌───────────────────┐ ┌──────────────────┐ ┌─────────────────────┐
│ MeepleAiMetrics   │ │ Redis Config     │ │ Redis ZSET          │
│ (OpenTelemetry)   │ │ (appsettings)    │ │ cache:freq:{gameId} │
└───────────────────┘ └──────────────────┘ └─────────────────────┘

┌──────────────────────────────────────────────────────────────────────┐
│ CacheWarmingService : BackgroundService                              │
│  - ExecuteAsync() → periodic timer (configurable schedule)           │
│  - WarmTopQueriesAsync() → IFrequencyTracker + IRagService           │
│  - WarmSingleQueryAsync() → parallel processing with concurrency     │
└──────────────────────────────────────────────────────────────────────┘
         │
         ▼
┌──────────────────────────────────────────────────────────────────────┐
│ Redis Cache (StackExchange.Redis)                                    │
│  - cache:{gameId}:{queryHash} → cached response                      │
│  - cache:freq:{gameId} → ZSET (queryHash → access count)             │
└──────────────────────────────────────────────────────────────────────┘
```

### Component Responsibilities

#### 1. AiResponseCacheService (Orchestrator)

**Role**: Central cache coordinator, delegates to specialized services

**Responsibilities**:
- Cache CRUD operations (Get, Set, Invalidate)
- Coordinate metrics recording via `ICacheMetricsRecorder`
- Invoke TTL calculation via `ITtlCalculationStrategy`
- Trigger frequency tracking via `IFrequencyTracker`
- OpenTelemetry span creation for all operations

**Dependencies**:
- `IConnectionMultiplexer` (Redis)
- `ICacheMetricsRecorder`
- `ITtlCalculationStrategy`
- `IFrequencyTracker`
- `ILogger<AiResponseCacheService>`

#### 2. CacheMetricsRecorder : ICacheMetricsRecorder

**Role**: Non-blocking metrics recording to OpenTelemetry

**Responsibilities**:
- Record cache hits/misses with dimensions (operation, outcome, key_type, cache_type)
- Record invalidation events with reason dimension
- Async fire-and-forget pattern (does not block cache operations)
- Error handling and fallback counting

**Dependencies**:
- `MeepleAiMetrics` (existing OTEL metrics class)
- `ILogger<CacheMetricsRecorder>`

#### 3. DynamicTtlStrategy : ITtlCalculationStrategy

**Role**: Calculate cache expiration time based on access frequency

**Responsibilities**:
- Retrieve frequency data from `IFrequencyTracker`
- Apply TTL calculation algorithm (linear, exponential, constant)
- Return TimeSpan for Redis expiration
- Support multiple strategies via configuration

**Algorithms**:
- **Constant**: Fixed TTL (default: 1 hour) for all keys
- **Linear**: TTL = base + (frequency × multiplier), capped at max
- **Exponential**: TTL = base × (1 + log₁₀(frequency + 1)), capped at max

**Dependencies**:
- `IFrequencyTracker`
- `IOptions<CacheOptimizationConfiguration>`
- `ILogger<DynamicTtlStrategy>`

#### 4. RedisFrequencyTracker : IFrequencyTracker

**Role**: Track query access frequency in Redis sorted set

**Responsibilities**:
- Increment access count for cache keys (`ZINCRBY`)
- Retrieve top N keys by frequency (`ZREVRANGE`)
- Get frequency for specific key (`ZSCORE`)
- Trim ZSET to max size (prevent unbounded growth)
- Atomic operations for thread safety

**Redis Key Structure**:
- ZSET key: `cache:freq:{gameId}`
- Members: `{queryHash}`
- Scores: `{accessCount}`

**Dependencies**:
- `IConnectionMultiplexer` (Redis)
- `ILogger<RedisFrequencyTracker>`

#### 5. CacheWarmingService : BackgroundService

**Role**: Proactively populate cache with high-value queries

**Responsibilities**:
- Run on configurable schedule (e.g., 2am-4am UTC)
- Retrieve top N queries from `IFrequencyTracker`
- Execute queries via `IRagService` to warm cache
- Parallel processing with configurable concurrency
- Circuit breaker: skip if system load high (CPU >70%)
- Comprehensive logging with correlation IDs

**Dependencies**:
- `IFrequencyTracker`
- `IRagService`
- `IServiceProvider` (scoped service factory)
- `IOptions<CacheOptimizationConfiguration>`
- `ILogger<CacheWarmingService>`

---

## Sequence Diagrams

### 1. Cache Hit Path with Metrics

```
Client          AiResponseCache    CacheMetrics    FrequencyTracker    Redis         OTEL
  │                    │                │                 │              │             │
  │──GetAsync()───────>│                │                 │              │             │
  │                    │─────StartActivity("cache.get")───┼──────────────┼────────────>│
  │                    │                │                 │              │             │
  │                    │──GET key──────────────────────────────────────>│             │
  │                    │<────value─────────────────────────────────────│             │
  │                    │                │                 │              │             │
  │                    │─RecordHitAsync()────>│           │              │             │
  │                    │  (Task.Run)    │     │           │              │             │
  │                    │                │     │─RecordCacheAccess(hit)──────────────>│
  │                    │                │     │           │              │             │
  │                    │─IncrementAsync(key)─────────────>│              │             │
  │                    │  (Task.Run)    │     │           │──ZINCRBY────>│             │
  │                    │                │     │           │<───count────│             │
  │                    │                │     │           │              │             │
  │<─response─────────│                │     │           │              │             │
  │                    │─────SetTag("outcome", "hit")─────┼──────────────┼────────────>│
  │                    │─────StopActivity()───────────────┼──────────────┼────────────>│
```

**Flow**:
1. Client calls `GetAsync(gameId, queryHash)`
2. Cache service starts OpenTelemetry Activity span
3. Attempt Redis GET (cache hit)
4. Fire-and-forget async: Record metrics to OTEL (non-blocking)
5. Fire-and-forget async: Increment frequency in Redis ZSET (non-blocking)
6. Return cached response to client
7. Stop Activity span with outcome tag

**Performance**:
- Synchronous path: Redis GET (~1ms)
- Async background: Metrics recording (~2ms) + frequency increment (~1ms)
- Total client-perceived latency: ~1ms

### 2. Cache Miss Path with Dynamic TTL

```
Client          AiResponseCache    CacheMetrics    TtlStrategy    FrequencyTracker    Redis         OTEL
  │                    │                │              │                 │              │             │
  │──GetAsync()───────>│                │              │                 │              │             │
  │                    │─────StartActivity("cache.get")┼─────────────────┼──────────────┼────────────>│
  │                    │                │              │                 │              │             │
  │                    │──GET key──────────────────────┼─────────────────┼─────────────>│             │
  │                    │<────null──────────────────────┼─────────────────┼──────────────│             │
  │                    │                │              │                 │              │             │
  │                    │─RecordMissAsync()────>│       │                 │              │             │
  │                    │  (Task.Run)    │      │       │                 │              │             │
  │                    │                │      │─RecordCacheAccess(miss)────────────────┼────────────>│
  │                    │                │      │       │                 │              │             │
  │<─null─────────────│                │      │       │                 │              │             │
  │                    │─────StopActivity()────┼───────┼─────────────────┼──────────────┼────────────>│
  │                    │                │      │       │                 │              │             │
  │─[Client computes  │                │      │       │                 │              │             │
  │  response]         │                │      │       │                 │              │             │
  │                    │                │      │       │                 │              │             │
  │──SetAsync()───────>│                │      │       │                 │              │             │
  │                    │─────StartActivity("cache.set")┼─────────────────┼──────────────┼────────────>│
  │                    │                │      │       │                 │              │             │
  │                    │─CalculateTtl(key)────────────>│                 │              │             │
  │                    │                │      │       │─GetFrequency(key)────────────>│             │
  │                    │                │      │       │<───count──────────────────────│             │
  │                    │                │      │       │[Apply algorithm: linear decay] │             │
  │                    │<─ttl (e.g., 30min)────────────│                 │              │             │
  │                    │                │      │       │                 │              │             │
  │                    │──SET key value EX ttl─────────┼─────────────────┼─────────────>│             │
  │                    │<────OK────────────────────────┼─────────────────┼──────────────│             │
  │                    │                │      │       │                 │              │             │
  │                    │─IncrementAsync(key)───────────┼─────────────────>│              │             │
  │                    │  (Task.Run)    │      │       │                 │──ZINCRBY────>│             │
  │                    │                │      │       │                 │<───count────│             │
  │                    │                │      │       │                 │              │             │
  │<─success──────────│                │      │       │                 │              │             │
  │                    │─────StopActivity()────┼───────┼─────────────────┼──────────────┼────────────>│
```

**Flow**:
1. Client calls `GetAsync()` → Cache miss (null)
2. Client computes response (calls RagService, LlmService, etc.)
3. Client calls `SetAsync(key, value)`
4. Cache service invokes `ITtlCalculationStrategy.CalculateTtl()`
5. TTL strategy retrieves frequency from Redis ZSET
6. TTL strategy applies algorithm (e.g., linear: `base + frequency * 5 minutes`)
7. Cache service sets key with calculated TTL in Redis
8. Fire-and-forget async: Increment frequency in ZSET
9. Return success to client

**Dynamic TTL Example**:
- Frequency = 0 (first access): TTL = 30 minutes (base)
- Frequency = 10 (popular query): TTL = 30 + (10 × 5) = 80 minutes
- Frequency = 100 (very popular): TTL = capped at 240 minutes (4 hours)

### 3. Cache Warming Flow

```
Timer           CacheWarmingService    FrequencyTracker    Redis       RagService    AiResponseCache
  │                     │                      │             │              │                │
  │─[2am UTC]──────────>│                      │             │              │                │
  │                     │─StartActivity("cache.warming")─────┼──────────────┼────────────────│
  │                     │                      │             │              │                │
  │                     │─CheckSystemLoad()────┼─────────────┼──────────────┼────────────────│
  │                     │  [CPU: 45%, OK]      │             │              │                │
  │                     │                      │             │              │                │
  │                     │─GetTopKeysAsync(100)────>│         │              │                │
  │                     │                      │─ZREVRANGE(0,99)────>│       │                │
  │                     │                      │<─[key1,key2,...]────│       │                │
  │                     │<─topKeys─────────────│             │              │                │
  │                     │                      │             │              │                │
  │                     │─[Parallel.ForEachAsync(maxConcurrency: 5)]────────┼────────────────│
  │                     │                      │             │              │                │
  │                     │──WarmQuery(key1)─────┼─────────────┼─────────────>│                │
  │                     │                      │             │              │─SearchAsync()─>│
  │                     │                      │             │              │<─result───────│
  │                     │                      │             │              │                │
  │                     │                      │             │              │──SetAsync()────>│
  │                     │                      │             │              │                │─SET key result
  │                     │                      │             │              │<─success───────│
  │                     │<─success─────────────┼─────────────┼──────────────│                │
  │                     │                      │             │              │                │
  │                     │──WarmQuery(key2)─────┼─────────────┼─────────────>│                │
  │                     │  [... parallel processing ...]     │              │                │
  │                     │                      │             │              │                │
  │                     │─[Log: Warmed 95/100, 5 errors, duration: 3m 24s]─┼────────────────│
  │                     │─StopActivity()───────┼─────────────┼──────────────┼────────────────│
  │                     │                      │             │              │                │
  │─[3am UTC]──────────>│─[Next run]──────────┼─────────────┼──────────────┼────────────────│
```

**Flow**:
1. PeriodicTimer triggers at configured schedule (e.g., 2am UTC)
2. Check system load (CPU, queue depth) → Circuit breaker
3. Retrieve top N keys from Redis ZSET (sorted by frequency)
4. Process queries in parallel (configurable concurrency, default: 5)
5. For each query:
   - Execute RagService.SearchAsync() to generate response
   - Call AiResponseCacheService.SetAsync() to cache result
6. Log summary: success count, error count, duration
7. Wait for next scheduled run (e.g., 1 hour later or next day)

**Error Handling**:
- Query-level errors logged but don't stop batch
- Circuit breaker: Skip entire run if CPU >70% or queue depth >1000
- Exponential backoff on consecutive failures

### 4. Cache Invalidation on PDF Update

```
Client          PdfStorageService    AiResponseCache    CacheMetrics    Redis         OTEL
  │                     │                    │                │             │             │
  │─UploadPdfAsync()──>│                    │                │             │             │
  │                     │─[Validate PDF]────┼────────────────┼─────────────┼─────────────│
  │                     │─[Save to storage]─┼────────────────┼─────────────┼─────────────│
  │                     │                    │                │             │             │
  │                     │─InvalidateByGameIdAsync(gameId)────>│             │             │
  │                     │                    │─StartActivity("cache.invalidate")──────────>│
  │                     │                    │                │             │             │
  │                     │                    │─KEYS cache:{gameId}:*───────┼────────────>│
  │                     │                    │<─[key1,key2,...]────────────│             │
  │                     │                    │                │             │             │
  │                     │                    │─DEL key1 key2 ...───────────┼────────────>│
  │                     │                    │<─deleted count──────────────│             │
  │                     │                    │                │             │             │
  │                     │                    │─DEL cache:freq:{gameId}─────┼────────────>│
  │                     │                    │<─OK─────────────────────────│             │
  │                     │                    │                │             │             │
  │                     │                    │─RecordInvalidationAsync(reason: "pdf_update")──>│
  │                     │                    │  (Task.Run)    │             │             │
  │                     │                    │                │─RecordCacheInvalidation()─────>│
  │                     │                    │                │             │             │
  │                     │                    │─StopActivity()─┼─────────────┼────────────>│
  │                     │<─invalidated count─│                │             │             │
  │                     │                    │                │             │             │
  │<─upload success────│                    │                │             │             │
```

**Flow**:
1. Client uploads new PDF for game
2. `PdfStorageService.UploadPdfAsync()` validates and saves PDF
3. PdfStorageService calls `AiResponseCacheService.InvalidateByGameIdAsync(gameId, "pdf_update")`
4. Cache service starts OpenTelemetry Activity span
5. Cache service finds all keys matching pattern `cache:{gameId}:*` (Redis KEYS or SCAN)
6. Cache service deletes all matching keys in batch (Redis DEL)
7. Cache service deletes frequency ZSET `cache:freq:{gameId}`
8. Fire-and-forget async: Record invalidation event to OTEL with reason
9. Stop Activity span with deleted count
10. Return invalidated count to PdfStorageService

**Invalidation Reasons** (dimension for metrics):
- `pdf_update` - New PDF uploaded
- `pdf_delete` - PDF deleted
- `manual` - Admin manual invalidation
- `ttl_expired` - Redis automatic expiration (not tracked)

---

## Interface Definitions

### ICacheMetricsRecorder

```csharp
namespace MeepleAI.Services;

/// <summary>
/// Records cache operation metrics to OpenTelemetry.
/// Implementations must not block cache operations (use async fire-and-forget).
/// </summary>
public interface ICacheMetricsRecorder
{
    /// <summary>
    /// Records a cache access (hit or miss).
    /// </summary>
    /// <param name="operation">Operation type (e.g., "get", "set")</param>
    /// <param name="outcome">Outcome (e.g., "hit", "miss")</param>
    /// <param name="keyType">Key type (e.g., "qa", "rag", "setup")</param>
    /// <param name="latencyMs">Operation latency in milliseconds</param>
    /// <param name="cacheType">Cache type (default: "redis")</param>
    void RecordCacheAccess(
        string operation,
        string outcome,
        string keyType,
        double latencyMs,
        string cacheType = "redis");

    /// <summary>
    /// Records a cache invalidation event.
    /// </summary>
    /// <param name="reason">Invalidation reason (e.g., "pdf_update", "manual")</param>
    /// <param name="keysInvalidated">Number of keys invalidated</param>
    /// <param name="cacheType">Cache type (default: "redis")</param>
    void RecordInvalidation(
        string reason,
        int keysInvalidated,
        string cacheType = "redis");

    /// <summary>
    /// Records a metrics recording error (fallback counter).
    /// </summary>
    void RecordMetricsError();
}
```

### ITtlCalculationStrategy

```csharp
namespace MeepleAI.Services;

/// <summary>
/// Calculates cache Time-To-Live (TTL) based on access frequency.
/// </summary>
public interface ITtlCalculationStrategy
{
    /// <summary>
    /// Calculates TTL for a cache key based on its access frequency.
    /// </summary>
    /// <param name="gameId">Game ID</param>
    /// <param name="queryHash">Query hash</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>TTL duration (capped by configuration)</returns>
    Task<TimeSpan> CalculateTtlAsync(
        Guid gameId,
        string queryHash,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the default TTL for new cache entries.
    /// </summary>
    TimeSpan GetDefaultTtl();
}
```

### IFrequencyTracker

```csharp
namespace MeepleAI.Services;

/// <summary>
/// Tracks cache key access frequency in Redis sorted set.
/// </summary>
public interface IFrequencyTracker
{
    /// <summary>
    /// Increments access count for a cache key.
    /// </summary>
    /// <param name="gameId">Game ID</param>
    /// <param name="queryHash">Query hash</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task IncrementAsync(
        Guid gameId,
        string queryHash,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets top N most frequently accessed keys for a game.
    /// </summary>
    /// <param name="gameId">Game ID</param>
    /// <param name="topN">Number of keys to retrieve</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of (queryHash, accessCount) tuples, sorted descending</returns>
    Task<List<(string QueryHash, double AccessCount)>> GetTopKeysAsync(
        Guid gameId,
        int topN,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets access count for a specific key.
    /// </summary>
    /// <param name="gameId">Game ID</param>
    /// <param name="queryHash">Query hash</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Access count (0 if key not found)</returns>
    Task<double> GetFrequencyAsync(
        Guid gameId,
        string queryHash,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes frequency data for a game (used during invalidation).
    /// </summary>
    /// <param name="gameId">Game ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task DeleteFrequencyDataAsync(
        Guid gameId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Trims frequency ZSET to max size (prevents unbounded growth).
    /// </summary>
    /// <param name="gameId">Game ID</param>
    /// <param name="maxSize">Maximum ZSET size</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Number of entries trimmed</returns>
    Task<long> TrimFrequencyDataAsync(
        Guid gameId,
        int maxSize,
        CancellationToken cancellationToken = default);
}
```

---

## Data Models

### Redis Key Structure

**Cache Keys** (existing):
```
Pattern: cache:{gameId}:{queryHash}
Example: cache:550e8400-e29b-41d4-a716-446655440000:abc123def456
Value: JSON serialized CachedResponse
Expiration: Dynamic TTL (30 minutes to 4 hours)
```

**Frequency Tracking** (new):
```
Pattern: cache:freq:{gameId}
Type: Redis Sorted Set (ZSET)
Members: {queryHash} (e.g., "abc123def456")
Scores: {accessCount} (double, incremented via ZINCRBY)
Example: cache:freq:550e8400-e29b-41d4-a716-446655440000
  Member: "abc123def456" → Score: 142.0 (accessed 142 times)
  Member: "xyz789ghi012" → Score: 87.0 (accessed 87 times)
No Expiration: Persistent (trimmed periodically to max size)
```

### Configuration Schema

```json
{
  "CacheOptimization": {
    "EnableMetrics": true,
    "EnableDynamicTtl": false,
    "EnableCacheWarming": false,
    "EnableFrequencyTracking": true,
    "TtlStrategy": {
      "Type": "linear",
      "BaseMinutes": 30,
      "MaxMinutes": 240,
      "LinearMultiplierMinutes": 5,
      "ExponentialBase": 1.5
    },
    "FrequencyTracking": {
      "MaxZSetSize": 10000,
      "TrimThreshold": 12000
    },
    "CacheWarming": {
      "Enabled": false,
      "ScheduleCron": "0 2 * * *",
      "TopQueriesCount": 100,
      "MaxConcurrency": 5,
      "TimeoutSeconds": 30,
      "CircuitBreakerCpuThreshold": 0.70,
      "CircuitBreakerQueueDepthThreshold": 1000
    }
  }
}
```

### Configuration Classes

```csharp
namespace MeepleAI.Configuration;

public class CacheOptimizationConfiguration
{
    public bool EnableMetrics { get; set; } = true;
    public bool EnableDynamicTtl { get; set; } = false;
    public bool EnableCacheWarming { get; set; } = false;
    public bool EnableFrequencyTracking { get; set; } = true;

    public TtlStrategyConfiguration TtlStrategy { get; set; } = new();
    public FrequencyTrackingConfiguration FrequencyTracking { get; set; } = new();
    public CacheWarmingConfiguration CacheWarming { get; set; } = new();
}

public class TtlStrategyConfiguration
{
    /// <summary>
    /// TTL calculation strategy: "constant", "linear", "exponential"
    /// </summary>
    public string Type { get; set; } = "linear";

    /// <summary>
    /// Base TTL in minutes (minimum for all keys)
    /// </summary>
    public int BaseMinutes { get; set; } = 30;

    /// <summary>
    /// Maximum TTL in minutes (cap for dynamic calculation)
    /// </summary>
    public int MaxMinutes { get; set; } = 240;

    /// <summary>
    /// Linear strategy: minutes added per access (TTL = base + frequency * multiplier)
    /// </summary>
    public int LinearMultiplierMinutes { get; set; } = 5;

    /// <summary>
    /// Exponential strategy: base multiplier (TTL = base * (1 + log(frequency + 1)))
    /// </summary>
    public double ExponentialBase { get; set; } = 1.5;
}

public class FrequencyTrackingConfiguration
{
    /// <summary>
    /// Maximum ZSET size before trimming (per game)
    /// </summary>
    public int MaxZSetSize { get; set; } = 10000;

    /// <summary>
    /// Trim when ZSET exceeds this threshold
    /// </summary>
    public int TrimThreshold { get; set; } = 12000;
}

public class CacheWarmingConfiguration
{
    /// <summary>
    /// Enable cache warming background service
    /// </summary>
    public bool Enabled { get; set; } = false;

    /// <summary>
    /// Cron expression for warming schedule (default: 2am daily)
    /// </summary>
    public string ScheduleCron { get; set; } = "0 2 * * *";

    /// <summary>
    /// Number of top queries to warm per game
    /// </summary>
    public int TopQueriesCount { get; set; } = 100;

    /// <summary>
    /// Maximum parallel query execution
    /// </summary>
    public int MaxConcurrency { get; set; } = 5;

    /// <summary>
    /// Timeout for each query execution in seconds
    /// </summary>
    public int TimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// Skip warming if CPU usage exceeds threshold (0.0-1.0)
    /// </summary>
    public double CircuitBreakerCpuThreshold { get; set; } = 0.70;

    /// <summary>
    /// Skip warming if queue depth exceeds threshold
    /// </summary>
    public int CircuitBreakerQueueDepthThreshold { get; set; } = 1000;
}
```

---

## Integration Points

### 1. Dependency Injection (Program.cs)

```csharp
// Cache optimization services registration
var cacheOptConfig = builder.Configuration.GetSection("CacheOptimization");
builder.Services.Configure<CacheOptimizationConfiguration>(cacheOptConfig);

// Core interfaces
builder.Services.AddSingleton<ICacheMetricsRecorder, CacheMetricsRecorder>();
builder.Services.AddSingleton<IFrequencyTracker, RedisFrequencyTracker>();
builder.Services.AddSingleton<ITtlCalculationStrategy, DynamicTtlStrategy>();

// Background service (conditionally registered based on config)
var cacheWarmingEnabled = cacheOptConfig.GetValue<bool>("CacheWarming:Enabled");
if (cacheWarmingEnabled)
{
    builder.Services.AddHostedService<CacheWarmingService>();
}

// Enhanced cache service (replaces existing registration)
builder.Services.AddSingleton<IAiResponseCacheService, AiResponseCacheService>();
```

### 2. OpenTelemetry Metrics (MeepleAiMetrics.cs)

**Existing Metrics** (already defined in OPS-02):
```csharp
// Cache metrics (existing)
private static readonly Counter<long> CacheHitsCounter = Meter.CreateCounter<long>(
    "meepleai.cache.hits.total",
    description: "Total number of cache hits");

private static readonly Counter<long> CacheMissesCounter = Meter.CreateCounter<long>(
    "meepleai.cache.misses.total",
    description: "Total number of cache misses");

private static readonly Counter<long> CacheEvictionsCounter = Meter.CreateCounter<long>(
    "meepleai.cache.evictions.total",
    description: "Total number of cache evictions");
```

**New Metrics** (to be added):
```csharp
// Cache operation histogram (NEW)
private static readonly Histogram<double> CacheOperationDuration = Meter.CreateHistogram<double>(
    "meepleai.cache.operation.duration",
    unit: "ms",
    description: "Cache operation latency in milliseconds");

// Cache invalidation counter (NEW)
private static readonly Counter<long> CacheInvalidationsCounter = Meter.CreateCounter<long>(
    "meepleai.cache.invalidations.total",
    description: "Total number of cache invalidation events");

// Metrics recording errors (NEW - fallback counter)
private static readonly Counter<long> MetricsErrorsCounter = Meter.CreateCounter<long>(
    "meepleai.cache.metrics.errors.total",
    description: "Total number of metrics recording errors");

// Public method for ICacheMetricsRecorder (NEW)
public static void RecordCacheAccess(
    string operation,
    string outcome,
    string keyType,
    double latencyMs,
    string cacheType = "redis")
{
    var tags = new TagList
    {
        { "operation", operation },
        { "outcome", outcome },
        { "key_type", keyType },
        { "cache_type", cacheType }
    };

    if (outcome == "hit")
        CacheHitsCounter.Add(1, tags);
    else if (outcome == "miss")
        CacheMissesCounter.Add(1, tags);

    CacheOperationDuration.Record(latencyMs, tags);
}

public static void RecordCacheInvalidation(string reason, int keysInvalidated, string cacheType = "redis")
{
    CacheInvalidationsCounter.Add(keysInvalidated, new TagList
    {
        { "reason", reason },
        { "cache_type", cacheType }
    });
}

public static void RecordMetricsError()
{
    MetricsErrorsCounter.Add(1);
}
```

### 3. OpenTelemetry Tracing (Activity Spans)

**Span Naming Convention**:
- `cache.get` - Cache retrieval operation
- `cache.set` - Cache write operation
- `cache.invalidate` - Cache invalidation operation
- `cache.warming` - Cache warming run
- `cache.frequency.increment` - Frequency tracking (optional, may be noisy)

**Span Tags**:
- `cache.operation` - Operation type (get, set, invalidate)
- `cache.outcome` - Outcome (hit, miss, success, error)
- `cache.key_type` - Key type (qa, rag, setup)
- `cache.game_id` - Game ID
- `cache.ttl_seconds` - TTL in seconds (for set operations)
- `cache.keys_invalidated` - Number of keys invalidated
- `cache.invalidation_reason` - Invalidation reason

**Example Span Creation**:
```csharp
using var activity = ActivitySource.StartActivity("cache.get");
activity?.SetTag("cache.game_id", gameId.ToString());
activity?.SetTag("cache.key_type", keyType);

try
{
    var result = await redis.StringGetAsync(cacheKey);
    var outcome = result.HasValue ? "hit" : "miss";
    activity?.SetTag("cache.outcome", outcome);
    return result;
}
catch (Exception ex)
{
    activity?.SetTag("cache.outcome", "error");
    activity?.SetTag("error", true);
    activity?.SetTag("error.message", ex.Message);
    throw;
}
```

### 4. Modified AiResponseCacheService Methods

**GetAsync() Enhancement**:
```csharp
public async Task<CachedResponse?> GetAsync(
    Guid gameId,
    string queryHash,
    CancellationToken cancellationToken = default)
{
    using var activity = _activitySource.StartActivity("cache.get");
    activity?.SetTag("cache.game_id", gameId.ToString());
    activity?.SetTag("cache.key_type", DetermineKeyType(queryHash));

    var stopwatch = Stopwatch.StartNew();
    var cacheKey = BuildCacheKey(gameId, queryHash);

    try
    {
        var db = _redis.GetDatabase();
        var json = await db.StringGetAsync(cacheKey);

        var outcome = json.HasValue ? "hit" : "miss";
        activity?.SetTag("cache.outcome", outcome);

        // Fire-and-forget metrics recording
        if (_config.EnableMetrics)
        {
            _ = Task.Run(() =>
            {
                try
                {
                    _metricsRecorder.RecordCacheAccess(
                        operation: "get",
                        outcome: outcome,
                        keyType: DetermineKeyType(queryHash),
                        latencyMs: stopwatch.Elapsed.TotalMilliseconds);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to record cache metrics");
                    _metricsRecorder.RecordMetricsError();
                }
            }, cancellationToken);
        }

        // Fire-and-forget frequency tracking (only on hit)
        if (_config.EnableFrequencyTracking && json.HasValue)
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    await _frequencyTracker.IncrementAsync(gameId, queryHash, cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to increment frequency for key {CacheKey}", cacheKey);
                }
            }, cancellationToken);
        }

        return json.HasValue
            ? JsonSerializer.Deserialize<CachedResponse>(json!)
            : null;
    }
    catch (Exception ex)
    {
        activity?.SetTag("cache.outcome", "error");
        _logger.LogError(ex, "Cache get failed for key {CacheKey}", cacheKey);
        throw;
    }
}
```

**SetAsync() Enhancement**:
```csharp
public async Task SetAsync(
    Guid gameId,
    string queryHash,
    CachedResponse response,
    CancellationToken cancellationToken = default)
{
    using var activity = _activitySource.StartActivity("cache.set");
    activity?.SetTag("cache.game_id", gameId.ToString());
    activity?.SetTag("cache.key_type", DetermineKeyType(queryHash));

    var stopwatch = Stopwatch.StartNew();
    var cacheKey = BuildCacheKey(gameId, queryHash);

    try
    {
        // Calculate dynamic TTL
        var ttl = _config.EnableDynamicTtl
            ? await _ttlStrategy.CalculateTtlAsync(gameId, queryHash, cancellationToken)
            : _ttlStrategy.GetDefaultTtl();

        activity?.SetTag("cache.ttl_seconds", (int)ttl.TotalSeconds);

        var db = _redis.GetDatabase();
        var json = JsonSerializer.Serialize(response);
        await db.StringSetAsync(cacheKey, json, ttl);

        activity?.SetTag("cache.outcome", "success");

        // Fire-and-forget frequency tracking
        if (_config.EnableFrequencyTracking)
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    await _frequencyTracker.IncrementAsync(gameId, queryHash, cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to increment frequency for key {CacheKey}", cacheKey);
                }
            }, cancellationToken);
        }
    }
    catch (Exception ex)
    {
        activity?.SetTag("cache.outcome", "error");
        _logger.LogError(ex, "Cache set failed for key {CacheKey}", cacheKey);
        throw;
    }
}
```

**InvalidateByGameIdAsync() Enhancement**:
```csharp
public async Task<int> InvalidateByGameIdAsync(
    Guid gameId,
    string reason,
    CancellationToken cancellationToken = default)
{
    using var activity = _activitySource.StartActivity("cache.invalidate");
    activity?.SetTag("cache.game_id", gameId.ToString());
    activity?.SetTag("cache.invalidation_reason", reason);

    try
    {
        var db = _redis.GetDatabase();
        var server = _redis.GetServer(_redis.GetEndPoints().First());

        // Find all matching keys
        var pattern = $"cache:{gameId}:*";
        var keys = server.Keys(pattern: pattern).ToArray();

        // Delete cache keys
        var deletedCount = keys.Length > 0
            ? await db.KeyDeleteAsync(keys)
            : 0;

        // Delete frequency data
        await _frequencyTracker.DeleteFrequencyDataAsync(gameId, cancellationToken);

        activity?.SetTag("cache.keys_invalidated", deletedCount);
        activity?.SetTag("cache.outcome", "success");

        // Fire-and-forget metrics recording
        if (_config.EnableMetrics)
        {
            _ = Task.Run(() =>
            {
                try
                {
                    _metricsRecorder.RecordInvalidation(reason, deletedCount);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to record cache invalidation metrics");
                    _metricsRecorder.RecordMetricsError();
                }
            }, cancellationToken);
        }

        _logger.LogInformation(
            "Invalidated {Count} cache entries for game {GameId}, reason: {Reason}",
            deletedCount, gameId, reason);

        return deletedCount;
    }
    catch (Exception ex)
    {
        activity?.SetTag("cache.outcome", "error");
        _logger.LogError(ex, "Cache invalidation failed for game {GameId}", gameId);
        throw;
    }
}
```

---

## Performance Considerations

### Latency Budget

| Operation | Target | Components |
|-----------|--------|------------|
| Cache Get (Hit) | <5ms | Redis GET (1ms) + Async metrics (2ms background) + Frequency incr (1ms background) |
| Cache Get (Miss) | <2ms | Redis GET (1ms) + Async metrics (1ms background) |
| Cache Set | <10ms | TTL calc (2ms) + Redis SET (2ms) + Async frequency (1ms background) |
| Invalidation | <50ms | Redis KEYS/SCAN (20ms) + DEL batch (10ms) + Frequency delete (5ms) |
| TTL Calculation | <100ms | Redis ZSCORE (10ms) + Algorithm (1ms) |

### Memory Overhead

**Frequency ZSET** (per game):
- 10k entries × 100 bytes per entry = ~1 MB
- Max 100 games with tracking = ~100 MB total
- Trimming at 12k entries keeps overhead bounded

**Event Loop**:
- Async metrics/frequency tracking use `Task.Run()` (thread pool)
- No blocking on cache critical path
- Exception handling prevents cascade failures

### Scalability Limits

**Current Architecture** (Option B):
- Supports ~1000 cache ops/sec with <5ms overhead
- Redis ZSET `ZINCRBY` is O(log N), scales to millions of members
- Cache warming with concurrency=5 processes 100 queries in ~3-5 minutes

**Bottlenecks**:
- Redis KEYS command in invalidation (use SCAN for >10k keys per game)
- Async fire-and-forget may drop metrics if thread pool saturated (monitor `ThreadPool.QueueUserWorkItem` failures)

**Future Optimization** (if needed):
- Batch frequency updates (buffer 100 ops, flush every 10s) → Reduce Redis calls by 10x
- Use Redis Streams for metrics events → Guaranteed delivery, replay capability
- Shard frequency data by game ID → Horizontal scaling

---

## Testing Strategy

### Unit Tests

**CacheMetricsRecorderTests.cs**:
- ✅ Verify metrics recording with correct dimensions
- ✅ Test async fire-and-forget pattern (metrics don't block)
- ✅ Test error handling (fallback counter incremented)

**DynamicTtlStrategyTests.cs**:
- ✅ Test constant strategy (always returns base TTL)
- ✅ Test linear strategy (base + frequency × multiplier, capped at max)
- ✅ Test exponential strategy (base × (1 + log(frequency + 1)))
- ✅ Test zero frequency (returns base TTL)
- ✅ Test high frequency (returns max TTL)

**RedisFrequencyTrackerTests.cs**:
- ✅ Test increment (ZINCRBY atomicity)
- ✅ Test GetTopKeys (ZREVRANGE with limit)
- ✅ Test GetFrequency (ZSCORE for specific member)
- ✅ Test DeleteFrequencyData (DEL entire ZSET)
- ✅ Test TrimFrequencyData (ZREMRANGEBYRANK)

**AiResponseCacheServiceTests.cs** (enhanced):
- ✅ Test GetAsync with metrics recorder mock
- ✅ Test SetAsync with TTL strategy mock
- ✅ Test InvalidateAsync with metrics recorder mock
- ✅ Test frequency tracking integration (mock IFrequencyTracker)

### Integration Tests

**CacheOptimizationIntegrationTests.cs**:
- ✅ Test end-to-end cache hit path (real Redis + Testcontainers)
- ✅ Test dynamic TTL calculation with real frequency data
- ✅ Test invalidation deletes cache + frequency data
- ✅ Test concurrent GetAsync/SetAsync (race condition validation)
- ✅ Test cache warming with mock RagService (verify batch processing)

**CacheWarmingServiceTests.cs**:
- ✅ Test periodic timer triggers warming
- ✅ Test circuit breaker skips warming when CPU high
- ✅ Test error handling (query failure doesn't stop batch)
- ✅ Test concurrency limit (max 5 parallel queries)

### Performance Tests

**CacheBenchmarkTests.cs** (BenchmarkDotNet):
- ✅ Benchmark GetAsync (hit) with/without metrics (target: <5ms)
- ✅ Benchmark SetAsync with dynamic TTL (target: <10ms)
- ✅ Benchmark frequency increment overhead (target: <1ms)
- ✅ Benchmark TTL calculation (target: <100ms)

### Observability Tests

**OpenTelemetryIntegrationTests.cs**:
- ✅ Test metrics exported to Prometheus endpoint
- ✅ Test Activity spans created for cache operations
- ✅ Test span tags populated correctly
- ✅ Test metrics dimensions (operation, outcome, key_type, cache_type, reason)

---

## Rollout Plan

### Phase 1: Foundation (Week 1)
**Goal**: Implement interfaces and frequency tracking (no behavior change)

**Tasks**:
1. Create interfaces (`ICacheMetricsRecorder`, `ITtlCalculationStrategy`, `IFrequencyTracker`)
2. Implement `RedisFrequencyTracker` with unit tests
3. Add configuration schema to `appsettings.json`
4. Update DI registration in `Program.cs`
5. Feature flag: `EnableFrequencyTracking: true`, others `false`

**Validation**:
- Unit tests pass (20+ tests)
- Redis ZSET populated after cache operations
- No performance regression (<5ms cache ops)

### Phase 2: Metrics & Dynamic TTL (Week 2)
**Goal**: Add observability and intelligent TTL calculation

**Tasks**:
1. Implement `CacheMetricsRecorder` (async fire-and-forget)
2. Implement `DynamicTtlStrategy` (linear algorithm)
3. Enhance `AiResponseCacheService` (GetAsync, SetAsync, InvalidateAsync)
4. Add OpenTelemetry Activity spans
5. Update `MeepleAiMetrics` with new counters/histograms
6. Feature flag: `EnableMetrics: true`, `EnableDynamicTtl: false` (manual testing)

**Validation**:
- Integration tests pass (15+ tests)
- Metrics visible in Prometheus `/metrics` endpoint
- Grafana dashboard shows cache hit rate, latency
- Dynamic TTL calculation correct (verify with logs)

### Phase 3: Cache Warming (Week 3)
**Goal**: Proactive cache population for high-value queries

**Tasks**:
1. Implement `CacheWarmingService` (BackgroundService)
2. Add circuit breaker (CPU/queue depth thresholds)
3. Add cron schedule parsing (default: 2am UTC)
4. Add parallel processing with concurrency limit
5. Feature flag: `EnableCacheWarming: false` (manual trigger endpoint)

**Validation**:
- Background service starts/stops correctly
- Manual trigger warms top 10 queries successfully
- Circuit breaker skips run when CPU high
- Errors logged but don't crash service

### Phase 4: Production Rollout (Week 4)
**Goal**: Enable features gradually, monitor metrics, tune TTL algorithm

**Tasks**:
1. Enable `EnableDynamicTtl: true` (monitor hit rate change)
2. Enable `EnableCacheWarming: true` (schedule 2am daily)
3. Monitor Grafana dashboard for 7 days
4. Measure success metrics (hit rate +10%, latency <5ms)
5. Tune TTL algorithm parameters based on data (adjust multiplier/max)

**Validation**:
- Cache hit rate increases by ≥10% over baseline
- Average cache operation latency <5ms (p95 <10ms)
- Redis memory usage stable (<10MB increase)
- Zero cache-related incidents

---

## Conclusion

This architecture design provides a comprehensive blueprint for AI-10 cache optimization. Key highlights:

**✅ Clean Architecture**: Layered design with interfaces enables testability and future extensibility
**✅ Non-Blocking Metrics**: Async fire-and-forget pattern ensures cache operations remain fast
**✅ Dynamic TTL**: Pluggable strategy pattern supports multiple algorithms (constant, linear, exponential)
**✅ Cache Warming**: Background service with circuit breaker and concurrency control
**✅ Observability**: OpenTelemetry spans + Prometheus metrics + Grafana dashboards
**✅ Risk Mitigation**: Feature flags, fallback counters, graceful degradation, comprehensive testing
**✅ Phased Rollout**: 4-week plan with validation gates at each phase

**Next Steps**:
1. Review architecture with team for feedback
2. Create implementation issues for each phase
3. Begin Phase 1 implementation (interfaces + frequency tracking)
4. Set up Grafana dashboard (import JSON from `infra/dashboards/`)

**Questions for Team**:
- Confirm TTL algorithm preference (linear vs exponential)?
- Preferred cache warming schedule (2am UTC or different time)?
- Any concerns about async fire-and-forget metrics?
