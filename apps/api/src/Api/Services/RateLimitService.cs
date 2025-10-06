using System.Globalization;
using StackExchange.Redis;

namespace Api.Services;

/// <summary>
/// Redis-based token bucket rate limiter.
/// Supports per-IP and per-user rate limiting with configurable limits.
/// </summary>
public class RateLimitService
{
    private readonly IConnectionMultiplexer _redis;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<RateLimitService> _logger;

    public RateLimitService(
        IConnectionMultiplexer redis,
        ILogger<RateLimitService> logger,
        TimeProvider? timeProvider = null)
    {
        _redis = redis;
        _logger = logger;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <summary>
    /// Check if a request is allowed under the rate limit.
    /// Uses token bucket algorithm with Redis for distributed rate limiting.
    /// </summary>
    /// <param name="key">Unique identifier for rate limit (e.g., IP address or user ID)</param>
    /// <param name="maxTokens">Maximum tokens in bucket (burst capacity)</param>
    /// <param name="refillRate">Tokens added per second</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Result with allowed status and retry-after seconds if rate limited</returns>
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

        // Lua script for atomic token bucket operations
        // Returns: [allowed (1/0), tokens_remaining, retry_after_seconds]
        var script = @"
            local tokens_key = KEYS[1]
            local last_refill_key = KEYS[2]
            local max_tokens = tonumber(ARGV[1])
            local refill_rate = tonumber(ARGV[2])
            local now = tonumber(ARGV[3])
            local cost = tonumber(ARGV[4])
            local ttl = tonumber(ARGV[5])

            local tokens = tonumber(redis.call('GET', tokens_key))
            local last_refill = tonumber(redis.call('GET', last_refill_key))

            if tokens == nil then
                tokens = max_tokens
                last_refill = now
            end

            local elapsed = now - last_refill
            local refilled_tokens = elapsed * refill_rate
            tokens = math.min(max_tokens, tokens + refilled_tokens)

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

            redis.call('SET', tokens_key, tokens, 'EX', ttl)
            redis.call('SET', last_refill_key, now, 'EX', ttl)

            return {allowed, math.floor(tokens), retry_after}
        ";

        var keys = new RedisKey[] { tokensKey, lastRefillKey };
        var values = new RedisValue[]
        {
            maxTokens,
            refillRate,
            now,
            1, // cost per request
            3600 // TTL: 1 hour (cleanup inactive buckets)
        };

        try
        {
            var result = await db.ScriptEvaluateAsync(script, keys, values);
            var resultArray = (RedisResult[])result!;
            var numericResults = Array.ConvertAll(resultArray, ConvertRedisResultToInt);

            var allowed = numericResults[0] == 1;
            var tokensRemaining = numericResults[1];
            var retryAfter = numericResults[2];

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

    /// <summary>
    /// Get rate limit configuration based on role or defaults.
    /// </summary>
    public static RateLimitConfig GetConfigForRole(string? role)
    {
        return role?.ToLowerInvariant() switch
        {
            "admin" => new RateLimitConfig(1000, 10.0), // 1000 burst, 10/sec
            "editor" => new RateLimitConfig(500, 5.0),   // 500 burst, 5/sec
            "user" => new RateLimitConfig(100, 1.0),     // 100 burst, 1/sec
            _ => new RateLimitConfig(60, 1.0)            // Default: 60 burst, 1/sec (anonymous)
        };
    }

    private static int ConvertRedisResultToInt(RedisResult result)
    {
        if (result.TryParse(out long longValue))
        {
            return (int)longValue;
        }

        var stringValue = result.ToString();

        if (stringValue is not null && int.TryParse(stringValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
        {
            return parsed;
        }

        throw new InvalidOperationException($"Unexpected Redis script result type: {result.Type}");
    }
}

/// <summary>
/// Result of a rate limit check.
/// </summary>
/// <param name="Allowed">Whether the request is allowed</param>
/// <param name="TokensRemaining">Number of tokens remaining in bucket</param>
/// <param name="RetryAfterSeconds">Seconds to wait before retrying (0 if allowed)</param>
public record RateLimitResult(bool Allowed, int TokensRemaining, int RetryAfterSeconds);

/// <summary>
/// Rate limit configuration.
/// </summary>
/// <param name="MaxTokens">Maximum tokens (burst capacity)</param>
/// <param name="RefillRate">Tokens added per second</param>
public record RateLimitConfig(int MaxTokens, double RefillRate);