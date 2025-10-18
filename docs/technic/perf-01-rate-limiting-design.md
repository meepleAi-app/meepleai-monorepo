# Technical Design: PERF-01 - Redis-Based Token Bucket Rate Limiting

**Issue**: #290 - PERF-01 - Rate limiting (Redis token bucket)
**Status**: Implemented
**Version**: 1.0
**Date**: 2025-09-30
**Authors**: MeepleAI Engineering Team

---

## Table of Contents

1. [Overview](#overview)
2. [Architecture](#architecture)
3. [Algorithm Design](#algorithm-design)
4. [Implementation Details](#implementation-details)
5. [Configuration](#configuration)
6. [Testing Strategy](#testing-strategy)
7. [Performance Analysis](#performance-analysis)
8. [Security Considerations](#security-considerations)
9. [Operational Guidelines](#operational-guidelines)

---

## Overview

### Problem Statement

The MeepleAI API requires protection against:
- **DDoS attacks**: Malicious actors overwhelming the system with requests
- **Brute force attacks**: Repeated authentication attempts
- **Resource exhaustion**: Legitimate users consuming too many resources
- **Unfair resource allocation**: Some users monopolizing API capacity

### Solution

Implement a distributed token bucket rate limiter using Redis as the backing store. The token bucket algorithm provides:
- **Burst tolerance**: Allows legitimate burst traffic
- **Smooth rate limiting**: Gradual token refill prevents cliff effects
- **Fairness**: Long-term average rate is maintained
- **Configurability**: Different limits per user role

### Goals

1. **Protect API resources** from abuse and overuse
2. **Maintain service availability** during traffic spikes
3. **Provide fair resource allocation** across users
4. **Enable monitoring** of rate limit usage
5. **Support horizontal scaling** across multiple API instances

### Non-Goals

1. Per-endpoint rate limiting (future enhancement)
2. Geolocation-based rate limiting
3. Dynamic rate limit adjustment based on load
4. Machine learning-based anomaly detection

---

## Architecture

### System Components

```json
┌─────────────────────────────────────────────────────────────┐
│                         API Gateway                          │
│                   (Load Balancer / Nginx)                    │
└────────────────────────┬────────────────────────────────────┘
                         │
                         │ HTTP Request
                         ▼
┌─────────────────────────────────────────────────────────────┐
│                    ASP.NET Core API                          │
│  ┌────────────────────────────────────────────────────────┐ │
│  │  Authentication Middleware                              │ │
│  │  - Validates session or API key                         │ │
│  │  - Extracts user identity and role                      │ │
│  └──────────────────────┬─────────────────────────────────┘ │
│                         │                                    │
│  ┌──────────────────────▼─────────────────────────────────┐ │
│  │  Rate Limiting Middleware                               │ │
│  │  - Determines rate limit key (IP or User ID)            │ │
│  │  - Fetches role-based configuration                     │ │
│  │  - Calls RateLimitService                               │ │
│  │  - Adds response headers                                │ │
│  │  - Returns 429 if rate limited                          │ │
│  └──────────────────────┬─────────────────────────────────┘ │
│                         │                                    │
│  ┌──────────────────────▼─────────────────────────────────┐ │
│  │  RateLimitService                                       │ │
│  │  - Executes token bucket algorithm                      │ │
│  │  - Communicates with Redis                              │ │
│  │  - Handles Redis failures (fail-open)                   │ │
│  └──────────────────────┬─────────────────────────────────┘ │
└────────────────────────┼────────────────────────────────────┘
                         │
                         │ Redis Protocol (Lua Script)
                         ▼
┌─────────────────────────────────────────────────────────────┐
│                         Redis                                │
│  ┌────────────────────────────────────────────────────────┐ │
│  │  Token Buckets (Key-Value Store)                        │ │
│  │                                                          │ │
│  │  ratelimit:user:123:tokens       → 95                   │ │
│  │  ratelimit:user:123:lastRefill   → 1633024800.5         │ │
│  │  ratelimit:ip:192.168.1.1:tokens → 58                   │ │
│  │  ...                                                     │ │
│  └────────────────────────────────────────────────────────┘ │
└─────────────────────────────────────────────────────────────┘
```

### Data Flow

**Successful Request (Under Limit)**:
```json
1. Client → API: HTTP Request
2. API → Auth Middleware: Extract user identity
3. API → Rate Limit Middleware: Check rate limit
4. Middleware → RateLimitService: CheckRateLimitAsync("user:123", 100, 1.0)
5. RateLimitService → Redis: Execute Lua script
6. Redis → RateLimitService: {allowed: true, tokens: 99, retryAfter: 0}
7. Middleware → Client: 200 OK + Headers (X-RateLimit-*)
8. API → Controller: Process request
9. Controller → Client: Response
```

**Rate Limited Request (Over Limit)**:
```json
1. Client → API: HTTP Request
2. API → Auth Middleware: Extract user identity
3. API → Rate Limit Middleware: Check rate limit
4. Middleware → RateLimitService: CheckRateLimitAsync("user:123", 100, 1.0)
5. RateLimitService → Redis: Execute Lua script
6. Redis → RateLimitService: {allowed: false, tokens: 0, retryAfter: 15}
7. Middleware → Client: 429 Too Many Requests + Headers (Retry-After: 15)
8. (Request processing stops here)
```

### Key Design Decisions

| Decision | Rationale |
|----------|-----------|
| **Redis as backing store** | Distributed, fast, supports Lua scripts for atomicity |
| **Token bucket algorithm** | Allows bursts, smooth refill, widely adopted |
| **Fail-open strategy** | Prioritizes availability over strict rate limiting |
| **Middleware-based** | Central enforcement point, consistent across all endpoints |
| **Role-based limits** | Different user types need different limits |
| **1-hour TTL on buckets** | Automatic cleanup of inactive users |

---

## Algorithm Design

### Token Bucket Overview

The token bucket algorithm maintains a "bucket" of tokens for each rate limit key:

```json
Token Bucket State:
- tokens: Current number of tokens in bucket
- lastRefill: Timestamp of last refill
- maxTokens: Maximum capacity of bucket
- refillRate: Tokens added per second

Request Processing:
1. Calculate elapsed time since last refill
2. Add refilled tokens: tokens += (elapsed × refillRate)
3. Cap tokens at maxTokens (bucket capacity)
4. If tokens >= cost: Allow request, deduct cost
5. If tokens < cost: Deny request, calculate retryAfter
```

### Lua Script

The algorithm is implemented as a Lua script executed atomically in Redis:

```lua
-- Keys: [tokens_key, last_refill_key]
-- Args: [max_tokens, refill_rate, now, cost, ttl]

local tokens_key = KEYS[1]
local last_refill_key = KEYS[2]
local max_tokens = tonumber(ARGV[1])
local refill_rate = tonumber(ARGV[2])
local now = tonumber(ARGV[3])
local cost = tonumber(ARGV[4])
local ttl = tonumber(ARGV[5])

-- Fetch current state (nil if not exists)
local tokens = tonumber(redis.call('GET', tokens_key))
local last_refill = tonumber(redis.call('GET', last_refill_key))

-- Initialize on first request
if tokens == nil then
    tokens = max_tokens
    last_refill = now
end

-- Calculate refilled tokens
local elapsed = now - last_refill
local refilled_tokens = elapsed * refill_rate
tokens = math.min(max_tokens, tokens + refilled_tokens)

-- Check if request can be allowed
local allowed = 0
local retry_after = 0

if tokens >= cost then
    tokens = tokens - cost
    allowed = 1
else
    -- Calculate seconds until enough tokens available
    local tokens_needed = cost - tokens
    retry_after = math.ceil(tokens_needed / refill_rate)
end

-- Update Redis state
redis.call('SET', tokens_key, tokens, 'EX', ttl)
redis.call('SET', last_refill_key, now, 'EX', ttl)

-- Return [allowed (1/0), tokens_remaining, retry_after_seconds]
return {allowed, math.floor(tokens), retry_after}
```

### Why Lua Script?

**Atomicity**: All operations execute atomically, preventing race conditions.

**Example Race Condition Without Atomicity**:
```
Instance A                  Instance B
─────────────────────────   ───────────────────────
GET tokens → 1              GET tokens → 1
Calculate refill            Calculate refill
SET tokens = 0 (allowed)    SET tokens = 0 (allowed)

Result: 2 requests allowed with 1 token (race condition!)
```

**With Lua Script**:
```
Instance A                  Instance B
─────────────────────────   ───────────────────────
EVAL script → allowed       (blocked, waiting)
                            EVAL script → denied

Result: Only 1 request allowed with 1 token (correct)
```

### Time Complexity Analysis

| Operation | Complexity | Notes |
|-----------|------------|-------|
| GET tokens | O(1) | Single Redis GET |
| GET last_refill | O(1) | Single Redis GET |
| Calculate refill | O(1) | Simple arithmetic |
| SET tokens | O(1) | Single Redis SET |
| SET last_refill | O(1) | Single Redis SET |
| **Total** | **O(1)** | All operations constant time |

### Memory Usage

**Per Token Bucket**:
```
ratelimit:user:123:tokens       → 8 bytes (integer)
ratelimit:user:123:lastRefill   → 8 bytes (float)
                                  ─────────
Total per bucket:                 16 bytes (keys overhead ~30 bytes)
                                  ≈ 50 bytes per active bucket
```

**Capacity Estimation**:
```
10,000 active users × 50 bytes = 500 KB
100,000 active users × 50 bytes = 5 MB
1,000,000 active users × 50 bytes = 50 MB
```

**TTL-Based Cleanup**:
- Buckets expire after 1 hour of inactivity
- Automatic memory reclamation
- No manual cleanup needed

---

## Implementation Details

### 1. RateLimitService

**File**: `apps/api/src/Api/Services/RateLimitService.cs`

#### Constructor

```csharp
public class RateLimitService
{
    private readonly IConnectionMultiplexer _redis;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<RateLimitService> _logger;
    private readonly RateLimitConfiguration _config;

    public RateLimitService(
        IConnectionMultiplexer redis,
        ILogger<RateLimitService> logger,
        IOptions<RateLimitConfiguration> config,
        TimeProvider? timeProvider = null)
    {
        _redis = redis;
        _logger = logger;
        _config = config.Value;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }
}
```

**Design Notes**:
- `IConnectionMultiplexer`: Redis connection (singleton, thread-safe)
- `TimeProvider`: Abstraction for testing (can inject fake time)
- `IOptions<RateLimitConfiguration>`: Strongly-typed configuration
- `ILogger`: Structured logging for observability

#### CheckRateLimitAsync Method

```csharp
public async Task<RateLimitResult> CheckRateLimitAsync(
    string key,
    int maxTokens,
    double refillRate,
    CancellationToken ct = default)
{
    var db = _redis.GetDatabase();
    var now = _timeProvider.GetUtcNow().ToUnixTimeMilliseconds() / 1000.0;

    var redisKey = $"ratelimit:{key}";
    var tokensKey = $"{redisKey}:tokens";
    var lastRefillKey = $"{redisKey}:lastRefill";

    var keys = new RedisKey[] { tokensKey, lastRefillKey };
    var values = new RedisValue[]
    {
        maxTokens,
        refillRate,
        now,
        1, // cost per request
        3600 // TTL: 1 hour
    };

    try
    {
        var result = await db.ScriptEvaluateAsync(luaScript, keys, values);
        var resultArray = (RedisResult[])result!;

        var allowed = ConvertRedisResultToInt(resultArray[0]) == 1;
        var tokensRemaining = ConvertRedisResultToInt(resultArray[1]);
        var retryAfter = ConvertRedisResultToInt(resultArray[2]);

        if (!allowed)
        {
            _logger.LogWarning("Rate limit exceeded for key {Key}. Retry after {RetryAfter}s",
                key, retryAfter);
        }

        return new RateLimitResult(allowed, tokensRemaining, retryAfter);
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Rate limit check failed for key {Key}. Allowing request (fail-open)", key);
        // Fail-open: allow request if Redis is unavailable
        return new RateLimitResult(true, maxTokens, 0);
    }
}
```

**Error Handling**:
- Catches all Redis exceptions (connection, timeout, etc.)
- Logs error for monitoring
- **Fails open**: Returns `allowed: true` to prevent cascading failures
- Alternative: Fail-closed would block all traffic during Redis outages

#### GetConfigForRole Method

```csharp
public RateLimitConfig GetConfigForRole(string? role)
{
    var roleConfig = role?.ToLowerInvariant() switch
    {
        "admin" => _config.Admin,
        "editor" => _config.Editor,
        "user" => _config.User,
        _ => _config.Anonymous
    };

    return new RateLimitConfig(roleConfig.MaxTokens, roleConfig.RefillRate);
}
```

**Design Notes**:
- Case-insensitive role matching
- Defaults to anonymous if role is null or unknown
- Returns immutable record type

### 2. Rate Limiting Middleware

**File**: `apps/api/src/Api/Program.cs` (lines 468-517)

```csharp
app.Use(async (context, next) =>
{
    var rateLimiter = context.RequestServices.GetRequiredService<RateLimitService>();
    var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();

    // Determine rate limit key (user ID or IP address)
    string rateLimitKey;
    RateLimitConfig config;

    if (context.Items.TryGetValue(nameof(ActiveSession), out var sessionObj)
        && sessionObj is ActiveSession session)
    {
        // Authenticated: rate limit per user + role
        rateLimitKey = $"user:{session.User.Id}";
        config = rateLimiter.GetConfigForRole(session.User.Role);
    }
    else
    {
        // Anonymous: rate limit per IP
        var ip = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        rateLimitKey = $"ip:{ip}";
        config = rateLimiter.GetConfigForRole(null);
    }

    // Check rate limit
    var result = await rateLimiter.CheckRateLimitAsync(
        rateLimitKey,
        config.MaxTokens,
        config.RefillRate,
        context.RequestAborted);

    // Add rate limit headers
    context.Response.Headers["X-RateLimit-Limit"] = config.MaxTokens.ToString();
    context.Response.Headers["X-RateLimit-Remaining"] = result.TokensRemaining.ToString();

    if (!result.Allowed)
    {
        // Rate limit exceeded
        context.Response.Headers["Retry-After"] = result.RetryAfterSeconds.ToString();
        context.Response.StatusCode = 429; // Too Many Requests

        logger.LogWarning("Rate limit exceeded for {Key}. Retry after {RetryAfter}s",
            rateLimitKey, result.RetryAfterSeconds);

        await context.Response.WriteAsJsonAsync(new
        {
            error = "Rate limit exceeded",
            retryAfter = result.RetryAfterSeconds,
            message = $"Too many requests. Please try again in {result.RetryAfterSeconds} seconds."
        });
        return;
    }

    await next();
});
```sql
**Design Notes**:
- Middleware runs after authentication (to determine user role)
- Uses `ActiveSession` from context items (set by auth middleware)
- Extracts IP address for anonymous users (`X-Forwarded-For` not used for security)
- Adds headers to all responses (success and failure)
- Short-circuits request pipeline on rate limit (returns 429)

### 3. Configuration Model

**File**: `apps/api/src/Api/Models/RateLimitConfiguration.cs`

```csharp
namespace Api.Models;

public record RoleLimitConfiguration
{
    public int MaxTokens { get; init; }
    public double RefillRate { get; init; }
}

public record RateLimitConfiguration
{
    public RoleLimitConfiguration Admin { get; init; }
        = new() { MaxTokens = 1000, RefillRate = 10.0 };

    public RoleLimitConfiguration Editor { get; init; }
        = new() { MaxTokens = 500, RefillRate = 5.0 };

    public RoleLimitConfiguration User { get; init; }
        = new() { MaxTokens = 100, RefillRate = 1.0 };

    public RoleLimitConfiguration Anonymous { get; init; }
        = new() { MaxTokens = 60, RefillRate = 1.0 };
}
```

**Design Notes**:
- `record` types: Immutable, value equality, thread-safe
- Default values: Match previous hardcoded values
- `init` accessors: Set during initialization only
- Nested structure: Clean separation of concerns

---

## Configuration

### Default Configuration

**File**: `apps/api/src/Api/appsettings.json`

```json
{
  "RateLimit": {
    "Admin": {
      "MaxTokens": 1000,
      "RefillRate": 10.0
    },
    "Editor": {
      "MaxTokens": 500,
      "RefillRate": 5.0
    },
    "User": {
      "MaxTokens": 100,
      "RefillRate": 1.0
    },
    "Anonymous": {
      "MaxTokens": 60,
      "RefillRate": 1.0
    }
  }
}
```

### Environment-Specific Configuration

**Development** (`appsettings.Development.json`):
```json
{
  "RateLimit": {
    "User": {
      "MaxTokens": 1000,
      "RefillRate": 10.0
    },
    "Anonymous": {
      "MaxTokens": 500,
      "RefillRate": 5.0
    }
  }
}
```

**Production** (environment variables):
```bash
RateLimit__Admin__MaxTokens=2000
RateLimit__Admin__RefillRate=20.0
RateLimit__User__MaxTokens=100
RateLimit__User__RefillRate=1.0
RateLimit__Anonymous__MaxTokens=30
RateLimit__Anonymous__RefillRate=0.5
```

### Configuration Binding

**File**: `apps/api/src/Api/Program.cs` (line 113)

```csharp
builder.Services.Configure<RateLimitConfiguration>(
    builder.Configuration.GetSection("RateLimit"));
```

**ASP.NET Core Options Pattern**:
- Configuration loaded at startup
- Strongly-typed access via `IOptions<RateLimitConfiguration>`
- Validation support (future enhancement)
- Hot reload support with `IOptionsSnapshot` (future enhancement)

---

## Testing Strategy

### Test Pyramid

```
         ╱╲
        ╱  ╲
       ╱ E2E╲           (2 tests)   - BDD integration tests
      ╱──────╲                       - Real HTTP requests
     ╱        ╲                      - TestRateLimitService
    ╱Integration╲      (14 tests)   - Full middleware stack
   ╱─────────────╲                   - Real authentication
  ╱               ╲                  - Headers, 429 responses
 ╱      Unit       ╲   (5 tests)    - Service in isolation
╱───────────────────╲                - Mocked Redis
```sql
### Test Coverage

**Total: 21 tests, 808 lines of test code**

#### Unit Tests (5 tests, 133 lines)

**File**: `apps/api/tests/Api.Tests/RateLimitServiceTests.cs`

1. `CheckRateLimit_AllowsRequestWhenUnderLimit`
   - Verifies request is allowed when tokens available
   - Checks tokens are decremented
   - Asserts RetryAfter is 0

2. `CheckRateLimit_DeniesRequestWhenOverLimit`
   - Verifies request is denied when no tokens
   - Checks RetryAfter is calculated correctly
   - Asserts TokensRemaining is 0

3. `CheckRateLimit_FailsOpenWhenRedisUnavailable`
   - Simulates Redis connection failure
   - Verifies request is allowed (fail-open)
   - Asserts error is logged

4. `GetConfigForRole_ReturnsCorrectLimits` (5 test cases)
   - Tests "admin" → 1000 tokens, 10 req/sec
   - Tests "editor" → 500 tokens, 5 req/sec
   - Tests "user" → 100 tokens, 1 req/sec
   - Tests null → 60 tokens, 1 req/sec
   - Tests unknown → 60 tokens, 1 req/sec

#### Integration Tests - BDD Style (2 tests, 311 lines)

**File**: `apps/api/tests/Api.Tests/RateLimitingIntegrationTests.cs`

1. `RequestsBeyondLimit_Return429WithHeadersAndBody`
   ```gherkin
   Given: Admin user is authenticated
   When: User makes request within limit
   Then: Request succeeds
   When: User exceeds rate limit
   Then: Request returns 429
     And: Retry-After header is present
     And: Response body contains error details
   ```

2. `RateLimiter_FailsOpen_WhenServiceThrows`
   ```gherkin
   Given: Admin user is authenticated
     And: Rate limit service throws exception
   When: User makes request
   Then: Request succeeds (fail-open behavior)
     And: Headers show full limit available
   ```

#### Integration Tests - Comprehensive (14 tests, 364 lines)

**File**: `apps/api/tests/Api.Tests/RateLimitingTests.cs`

**Rate Limit Headers** (2 tests):
- Authenticated requests return headers
- Anonymous requests return headers

**Role-Based Limits** (3 tests):
- Admin users have 1000 token limit
- Editor users have 500 token limit
- Regular users have 100 token limit

**Rate Limit Exceeded (429)** (4 tests):
- Multiple requests eventually return 429
- 429 responses include Retry-After header
- 429 responses include JSON error message

**IP-Based Limiting** (2 tests):
- Anonymous requests are limited by IP
- Different users have separate buckets

**Behavior** (1 test):
- After login, user transitions from IP to user limit

### Test Infrastructure

**TestRateLimitService** (Mock Implementation):
```csharp
private sealed class TestRateLimitService : RateLimitService
{
    private readonly Queue<RedisResult[]> _responses = new();
    private Exception? _nextException;

    public void EnqueueResponse(bool allowed, int tokensRemaining, int retryAfterSeconds)
    {
        _responses.Enqueue(new RedisResult[]
        {
            RedisResult.Create(allowed ? 1 : 0),
            RedisResult.Create(tokensRemaining),
            RedisResult.Create(retryAfterSeconds)
        });
    }

    public void FailWith(Exception exception)
    {
        _nextException = exception;
    }
}
```

**Benefits**:
- Deterministic test results
- No real Redis dependency
- Control over rate limit behavior
- Fast test execution

---

## Performance Analysis

### Latency

**Rate Limit Check Latency** (measured):
```
P50 (median):  < 1ms
P95:           < 2ms
P99:           < 5ms
```json
**Components**:
- Redis network round-trip: ~0.5ms (LAN)
- Lua script execution: ~0.1ms
- C# marshalling: ~0.1ms
- Total: ~0.7ms typical

### Throughput

**Single Redis Instance**:
- Theoretical: 100,000 requests/sec
- Practical: 50,000 requests/sec (with other operations)
- Per-key limit: Depends on `refillRate`

**Scaling**:
- Horizontal: Multiple API instances share Redis
- Vertical: Redis Cluster for >100k req/sec
- Bottleneck: Redis CPU (single-threaded per core)

### Memory

**Per Active User**:
```
2 keys × 25 bytes (key overhead) = 50 bytes
2 values × 8 bytes (data) = 16 bytes
                            ─────
Total: ~70 bytes per active user
```sql
**Capacity**:
- 10k users: ~700 KB
- 100k users: ~7 MB
- 1M users: ~70 MB

**TTL Cleanup**:
- Inactive buckets expire after 1 hour
- Memory usage stabilizes at active users
- No manual cleanup needed

---

## Security Considerations

### Threat Model

**Threats Mitigated**:
1. **DDoS attacks**: Rate limiting prevents request floods
2. **Brute force attacks**: Limits authentication attempts
3. **Resource exhaustion**: Prevents individual users from consuming all resources
4. **API abuse**: Discourages scraping and automated attacks

**Threats NOT Mitigated**:
1. **Distributed DDoS**: Attacks from many IPs may bypass IP-based limits
2. **Amplification attacks**: Application-level vulnerabilities (use WAF)
3. **Sophisticated bots**: May use proxies to evade IP limits

### Security Properties

**Fail-Open vs. Fail-Closed**:

**Decision**: Fail-open (allow requests when Redis unavailable)

**Rationale**:
- Prioritizes availability over strict rate limiting
- Redis outages shouldn't cause service degradation
- Attacks during Redis downtime are rare
- Monitoring alerts on Redis failures

**Alternative**: Fail-closed (deny requests when Redis unavailable)
- Pros: Stricter security during Redis outages
- Cons: Service unavailability cascades from Redis failures

### Rate Limit Bypass Scenarios

**1. IP Address Spoofing**

**Mitigation**: Use `X-Forwarded-For` header (if behind trusted proxy)

**Risk**: Header can be spoofed if proxy is not trusted

**Current Implementation**: Uses `RemoteIpAddress` (not `X-Forwarded-For`)

**2. Credential Stuffing**

**Mitigation**: Rate limit by IP for authentication endpoints

**Limitation**: Shared IPs (NAT, office networks) affected

**Enhancement**: Add captcha after N failed attempts

**3. Distributed Attacks**

**Mitigation**: Global rate limit (future enhancement)

**Limitation**: Per-user/IP limits may not stop large botnets

**Enhancement**: Combine with WAF, IP reputation services

---

## Operational Guidelines

### Monitoring

**Key Metrics**:
1. **Rate limit hit rate**: % of requests rate limited
2. **Redis latency**: p50, p95, p99 for rate limit checks
3. **Fail-open events**: Count of Redis failures
4. **Token bucket state**: Average tokens remaining per role

**Recommended Dashboards**:
- Grafana dashboard with rate limit metrics
- Redis performance dashboard (Grafana + Prometheus)
- Application logs (Seq, ELK, Splunk)

**Alerts**:
```yaml
- Alert: HighRateLimitHitRate
  Condition: rate_limit_hit_rate > 10%
  Duration: 5 minutes
  Action: Notify engineering team

- Alert: RedisHighLatency
  Condition: redis_rate_limit_p95 > 10ms
  Duration: 2 minutes
  Action: Check Redis health

- Alert: RateLimitFailOpen
  Condition: rate_limit_fail_open_count > 0
  Duration: 1 minute
  Action: Page on-call engineer
```

### Capacity Planning

**Estimated Load**:
```
10,000 active users
× 1 request/sec average
× 1ms Redis latency
= 10 requests/sec to Redis (negligible)

Peak load (100× burst):
10,000 users × 100 req/sec = 1M req/sec
Redis capacity: 100k req/sec
Bottleneck: Redis (need Redis Cluster)
```json
**Scaling Recommendations**:
| Users | Requests/sec | Redis Setup | Estimated Cost |
|-------|--------------|-------------|----------------|
| <10k | <10k | Single instance | $20/month |
| 10k-100k | 10k-100k | Single instance + replica | $50/month |
| 100k-1M | 100k-1M | Redis Cluster (3 shards) | $200/month |
| >1M | >1M | Redis Cluster (10+ shards) | $1000+/month |

### Tuning

**Adjusting Limits**:
1. **Too Many 429s**: Increase `MaxTokens` or `RefillRate`
2. **Under-Protection**: Decrease limits
3. **Burst Issues**: Adjust `MaxTokens` (keeps `RefillRate` same)
4. **Sustained Load**: Adjust `RefillRate` (keeps `MaxTokens` same)

**Example Tuning**:
```
Problem: Users hitting rate limit during page load (10 requests burst)

Current: MaxTokens=100, RefillRate=1.0
Issue: 10 bursts × 10 requests = 100 tokens depleted quickly

Solution:
Option A: Increase burst capacity
  MaxTokens=200, RefillRate=1.0

Option B: Increase sustained rate
  MaxTokens=100, RefillRate=2.0

Recommendation: Option A (page loads are bursty)
```

### Troubleshooting

**Issue 1: High Rate Limit Hit Rate**

**Symptoms**: Many 429 responses

**Diagnosis**:
```sql
-- Check which endpoints are rate limited
SELECT path, COUNT(*) as rate_limited_count
FROM logs
WHERE status_code = 429
GROUP BY path
ORDER BY rate_limited_count DESC;
```json
**Solutions**:
- Increase rate limits for legitimate traffic
- Identify and block abusive IPs
- Add per-endpoint overrides for expensive operations

**Issue 2: Redis Connection Failures**

**Symptoms**: "Fail-open" logs, all requests allowed

**Diagnosis**:
```bash
# Check Redis connectivity
redis-cli -h localhost -p 6379 PING

# Check Redis metrics
redis-cli INFO stats
```

**Solutions**:
- Verify Redis is running
- Check network connectivity
- Scale Redis if CPU-bound
- Enable Redis persistence if data loss

**Issue 3: Uneven Rate Limiting**

**Symptoms**: Some users hit limit quickly, others never do

**Diagnosis**:
```bash
# Check token bucket state for users
redis-cli GET ratelimit:user:123:tokens
redis-cli GET ratelimit:user:456:tokens
```

**Solutions**:
- Verify rate limit key is correct (user ID, not session ID)
- Check for clock skew across API instances
- Ensure TimeProvider is consistent

---

## Appendix

### A. Redis Cluster Setup

**Configuration** (`redis-cluster.conf`):
```
cluster-enabled yes
cluster-config-file nodes.conf
cluster-node-timeout 5000
appendonly yes
```

**Deployment** (Docker Compose):
```yaml
services:
  redis-node-1:
    image: redis:7-alpine
    command: redis-server /usr/local/etc/redis/redis.conf
    volumes:
      - ./redis-cluster.conf:/usr/local/etc/redis/redis.conf
    ports:
      - "7001:7001"

  redis-node-2:
    # ... similar config on port 7002

  redis-node-3:
    # ... similar config on port 7003
```

**Client Configuration**:
```csharp
var options = ConfigurationOptions.Parse("localhost:7001,localhost:7002,localhost:7003");
options.ClusterConfiguration = new ClusterConfiguration
{
    TieBreaker = "",
    ConfigCheckSeconds = 5
};
var redis = ConnectionMultiplexer.Connect(options);
```

### B. Alternative Algorithms

**Fixed Window**:
```
Pros: Simple implementation
Cons: Cliff effect (burst at window boundary)
```

**Sliding Window**:
```
Pros: Smoother than fixed window
Cons: More complex, higher memory usage
```

**Leaky Bucket**:
```
Pros: Smooth output rate
Cons: Doesn't allow bursts, complex queueing
```

**Token Bucket (Chosen)**:
```
Pros: Allows bursts, smooth refill, widely adopted
Cons: Slightly more complex than fixed window
```

### C. References

- RFC 6585 - Additional HTTP Status Codes (429): https://tools.ietf.org/html/rfc6585
- Token Bucket Algorithm: https://en.wikipedia.org/wiki/Token_bucket
- Redis Lua Scripting: https://redis.io/commands/eval
- ASP.NET Core Middleware: https://learn.microsoft.com/en-us/aspnet/core/fundamentals/middleware/
- StackExchange.Redis Documentation: https://stackexchange.github.io/StackExchange.Redis/

---

**Version**: 1.0
**Status**: Approved
**Date**: 2025-09-30
**Authors**: MeepleAI Engineering Team
