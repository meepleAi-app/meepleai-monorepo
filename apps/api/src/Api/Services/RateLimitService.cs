using System.Globalization;
using Api.Models;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

#pragma warning disable MA0048 // File name must match type name - Contains Service with Configuration classes
namespace Api.Services;

/// <summary>
/// Redis-based token bucket rate limiter.
/// Supports per-IP and per-user rate limiting with configurable limits.
/// </summary>
public class RateLimitService : IRateLimitService
{
    private readonly IConnectionMultiplexer _redis;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<RateLimitService> _logger;
    private readonly RateLimitConfiguration _config;
    private readonly IConfigurationService? _configService;
    private readonly IConfiguration? _fallbackConfig;
    private readonly IWebHostEnvironment _environment;

    public RateLimitService(
        IConnectionMultiplexer redis,
        ILogger<RateLimitService> logger,
        IOptions<RateLimitConfiguration> config,
        IWebHostEnvironment environment,
        TimeProvider? timeProvider = null,
        IConfigurationService? configService = null,
        IConfiguration? fallbackConfig = null)
    {
        _redis = redis;
        _logger = logger;
        _config = config.Value;
        _environment = environment;
        _timeProvider = timeProvider ?? TimeProvider.System;
        _configService = configService;
        _fallbackConfig = fallbackConfig;
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
            var result = await db.ScriptEvaluateAsync(script, keys, values).ConfigureAwait(false);
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
#pragma warning disable CA1031 // Do not catch general exception types
        // Justification: Fail-open resilience pattern - rate limiting failures must not block legitimate traffic
        // We favor availability over strict rate enforcement during infrastructure failures
        catch (Exception ex)
        {
            // FAIL-OPEN PATTERN: Rate limiting failures must not block legitimate traffic
            // Rationale: Rate limiting is a protective control - failing closed when Redis is
            // unavailable creates a self-inflicted denial of service. We favor availability over
            // strict rate enforcement during infrastructure failures. Monitoring alerts on this
            // error enable operators to detect and resolve Redis issues without impacting users.
            // Context: Redis failures are typically transient (network blip, container restart)
            _logger.LogError(ex, "Rate limit check failed for key {Key}. Allowing request (fail-open)", key);
            return new RateLimitResult(true, maxTokens, 0);
        }
#pragma warning restore CA1031
    }

    /// <summary>
    /// Get rate limit configuration based on role with database configuration support.
    /// Fallback chain: DB (role-specific) → DB (global) → appsettings.json → hardcoded defaults.
    /// </summary>
    /// <param name="role">User role (admin, editor, user, anonymous)</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Rate limit configuration for the role</returns>
    public async Task<RateLimitConfig> GetConfigForRoleAsync(string? role, CancellationToken ct = default)
    {
        // For backward compatibility and when ConfigurationService is not available
        if (_configService is null)
        {
            return GetConfigFromHardcodedDefaults(role);
        }

        var normalizedRole = role?.ToLowerInvariant() ?? "anonymous";

        // Get both maxTokens and refillRate from database with fallback
        var maxTokens = await GetRateLimitValueAsync<int>("MaxTokens", normalizedRole, ct).ConfigureAwait(false);
        var refillRate = await GetRateLimitValueAsync<double>("RefillRate", normalizedRole, ct).ConfigureAwait(false);

        _logger.LogDebug("Rate limit config for {Role}: MaxTokens={MaxTokens}, RefillRate={RefillRate}",
            normalizedRole, maxTokens, refillRate);

        return new RateLimitConfig(maxTokens, refillRate);
    }

    /// <summary>
    /// Get a specific rate limit value with fallback chain.
    /// Issue #1663: Applies 10x multiplier in Dev/Test environments for K6 performance testing.
    /// </summary>
    private async Task<T> GetRateLimitValueAsync<T>(string limitType, string role) where T : struct
    {
        // Guard: This method should only be called when _configService is not null
        if (_configService == null)
        {
            throw new InvalidOperationException("ConfigurationService is required but was not provided");
        }

        // 1. Try DB config with role-specific key (e.g., RateLimit.MaxTokens.admin)
        var roleKey = $"RateLimit.{limitType}.{role}";
        var value = await _configService.GetValueAsync<T?>(roleKey).ConfigureAwait(false);
        if (value.HasValue)
        {
            var validated = ValidateRateLimit(value.Value, limitType, role);
            var multiplied = ApplyEnvironmentMultiplier(validated, limitType, role, "DB role-specific");
            _logger.LogInformation("Rate limit {LimitType} for {Role}: {Value} (from DB role-specific)",
                limitType, role, multiplied);
            return multiplied;
        }

        // 2. Try DB config with global key (e.g., RateLimit.MaxTokens)
        var globalKey = $"RateLimit.{limitType}";
        value = await _configService.GetValueAsync<T?>(globalKey).ConfigureAwait(false);
        if (value.HasValue)
        {
            var validated = ValidateRateLimit(value.Value, limitType, role);
            var multiplied = ApplyEnvironmentMultiplier(validated, limitType, role, "DB global");
            _logger.LogInformation("Rate limit {LimitType} for {Role}: {Value} (from DB global)",
                limitType, role, multiplied);
            return multiplied;
        }

        // 3. Try appsettings.json (backward compatibility)
        if (_fallbackConfig is not null)
        {
            var appsettingsValue = _fallbackConfig.GetValue<T?>($"RateLimiting:{limitType}:{role}");
            if (appsettingsValue.HasValue)
            {
                var validated = ValidateRateLimit(appsettingsValue.Value, limitType, role);
                var multiplied = ApplyEnvironmentMultiplier(validated, limitType, role, "appsettings");
                _logger.LogInformation("Rate limit {LimitType} for {Role}: {Value} (from appsettings)",
                    limitType, role, multiplied);
                return multiplied;
            }
        }

        // 4. Hardcoded defaults
        var defaultValue = GetHardcodedDefault<T>(limitType, role);
        var multipliedDefault = ApplyEnvironmentMultiplier(defaultValue, limitType, role, "hardcoded default");
        _logger.LogWarning("Rate limit {LimitType} for {Role}: {Value} (using hardcoded default)",
            limitType, role, multipliedDefault);
        return multipliedDefault;
    }

    /// <summary>
    /// Validate rate limit values to ensure they are within acceptable bounds.
    /// </summary>
    private T ValidateRateLimit<T>(T value, string limitType, string role) where T : struct
    {
        // For integer types (MaxTokens)
        if (value is int intValue)
        {
            if (intValue <= 0)
            {
                _logger.LogError("Rate limit {LimitType} for {Role} must be positive, got {Value}. Using hardcoded default.",
                    limitType, role, intValue);
                return GetHardcodedDefault<T>(limitType, role);
            }

            // Reasonable upper bound to prevent misconfiguration
            const int maxTokensUpperBound = 100000;
            if (string.Equals(limitType, "MaxTokens", StringComparison.Ordinal) && intValue > maxTokensUpperBound)
            {
                _logger.LogWarning("Rate limit {LimitType} value {Value} exceeds maximum {MaxLimit}, capping",
                    limitType, intValue, maxTokensUpperBound);
                return (T)(object)maxTokensUpperBound;
            }
        }

        // For double types (RefillRate)
        if (value is double doubleValue)
        {
            if (doubleValue <= 0)
            {
                _logger.LogError("Rate limit {LimitType} for {Role} must be positive, got {Value}. Using hardcoded default.",
                    limitType, role, doubleValue);
                return GetHardcodedDefault<T>(limitType, role);
            }

            // Reasonable upper bound
            const double refillRateUpperBound = 1000.0;
            if (string.Equals(limitType, "RefillRate", StringComparison.Ordinal) && doubleValue > refillRateUpperBound)
            {
                _logger.LogWarning("Rate limit {LimitType} value {Value} exceeds maximum {MaxLimit}, capping",
                    limitType, doubleValue, refillRateUpperBound);
                return (T)(object)refillRateUpperBound;
            }
        }

        return value;
    }

    /// <summary>
    /// Apply environment-specific multiplier to rate limit values.
    /// Issue #1663: 10x multiplier for Development/Test environments for K6 performance testing.
    /// </summary>
    private T ApplyEnvironmentMultiplier<T>(T value, string limitType, string role, string source) where T : struct
    {
        var isTestEnvironment = _environment.IsDevelopment() || string.Equals(_environment.EnvironmentName, "Test", StringComparison.Ordinal);
        if (!isTestEnvironment)
        {
            return value;
        }

        const int multiplier = 10;

        T multipliedValue = value switch
        {
            int intValue => (T)(object)(intValue * multiplier),
            double doubleValue => (T)(object)(doubleValue * multiplier),
            _ => value
        };

        _logger.LogDebug(
            "Applied 10x Dev/Test multiplier to rate limit {LimitType} for {Role}: {Original} → {Multiplied} (from {Source})",
            limitType, role, value, multipliedValue, source);

        return multipliedValue;
    }

    /// <summary>
    /// Get hardcoded default values for rate limits (base values without multiplier).
    /// </summary>
    private static T GetHardcodedDefault<T>(string limitType, string role) where T : struct
    {
        return (limitType, role) switch
        {
            ("MaxTokens", "admin") => (T)(object)1000,
            ("MaxTokens", "editor") => (T)(object)500,
            ("MaxTokens", "user") => (T)(object)100,
            ("MaxTokens", "anonymous") => (T)(object)60,
            ("RefillRate", "admin") => (T)(object)10.0,
            ("RefillRate", "editor") => (T)(object)5.0,
            ("RefillRate", "user") => (T)(object)1.0,
            ("RefillRate", "anonymous") => (T)(object)1.0,
            _ => throw new ArgumentException($"Unknown limit type {limitType} or role {role}", nameof(limitType))
        };
    }

    /// <summary>
    /// Get configuration from hardcoded defaults (fallback when ConfigurationService not available).
    /// Issue #1663: Applies 10x multiplier in Dev/Test environments.
    /// </summary>
    private RateLimitConfig GetConfigFromHardcodedDefaults(string? role)
    {
        var normalizedRole = role?.ToLowerInvariant() ?? "anonymous";

        var roleConfig = normalizedRole switch
        {
            "admin" => _config.Admin,
            "editor" => _config.Editor,
            "user" => _config.User,
            _ => _config.Anonymous
        };

        // Apply environment multiplier for Dev/Test (Issue #1663)
        var maxTokens = ApplyEnvironmentMultiplier(roleConfig.MaxTokens, "MaxTokens", normalizedRole, "injected config");
        var refillRate = ApplyEnvironmentMultiplier(roleConfig.RefillRate, "RefillRate", normalizedRole, "injected config");

        _logger.LogDebug("Using hardcoded rate limit config for {Role} (ConfigurationService not available)", normalizedRole);
        return new RateLimitConfig(maxTokens, refillRate);
    }

    private static int ConvertRedisResultToInt(RedisResult result)
    {
        if (result.Resp3Type == ResultType.Integer || result.Resp2Type == ResultType.Integer)
        {
            var longValue = (long)result;
            return (int)longValue;
        }

        var stringValue = result.ToString();

        if (stringValue is not null && int.TryParse(stringValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
        {
            return parsed;
        }

        throw new InvalidOperationException($"Unexpected Redis script result type: Resp3Type={result.Resp3Type}, Resp2Type={result.Resp2Type}");
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
