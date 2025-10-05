using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using StackExchange.Redis;

namespace Api.Services;

/// <summary>
/// AI-05: Redis-based cache for AI responses
/// Caches QA, Explain, and Setup responses to reduce latency for popular queries.
/// </summary>
public class AiResponseCacheService : IAiResponseCacheService
{
    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<AiResponseCacheService> _logger;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };
    private const string DeleteByPatternScript = @"
local pattern = ARGV[1]
local cursor = '0'
local total = 0
repeat
    local result = redis.call('SCAN', cursor, 'MATCH', pattern, 'COUNT', 1000)
    cursor = result[1]
    local keys = result[2]
    local count = #keys
    if count > 0 then
        redis.call('DEL', unpack(keys))
        total = total + count
    end
until cursor == '0'
return total";

    public AiResponseCacheService(
        IConnectionMultiplexer redis,
        ILogger<AiResponseCacheService> logger)
    {
        _redis = redis;
        _logger = logger;
    }

    public async Task<T?> GetAsync<T>(string cacheKey, CancellationToken ct = default) where T : class
    {
        try
        {
            var db = _redis.GetDatabase();
            var cached = await db.StringGetAsync(cacheKey);

            if (!cached.HasValue)
            {
                _logger.LogDebug("Cache miss for key: {CacheKey}", cacheKey);
                return null;
            }

            _logger.LogInformation("Cache hit for key: {CacheKey}", cacheKey);
            var response = JsonSerializer.Deserialize<T>(cached.ToString(), JsonOptions);
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Cache get failed for key {CacheKey}. Proceeding without cache.", cacheKey);
            return null; // Fail gracefully
        }
    }

    public async Task SetAsync<T>(string cacheKey, T response, int ttlSeconds = 86400, CancellationToken ct = default) where T : class
    {
        try
        {
            var db = _redis.GetDatabase();
            var json = JsonSerializer.Serialize(response, JsonOptions);
            await db.StringSetAsync(cacheKey, json, TimeSpan.FromSeconds(ttlSeconds));
            _logger.LogInformation("Cached response for key: {CacheKey} (TTL: {TTL}s)", cacheKey, ttlSeconds);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Cache set failed for key {CacheKey}. Proceeding without cache.", cacheKey);
            // Fail gracefully - don't throw
        }
    }

    public string GenerateQaCacheKey(string gameId, string query)
    {
        var queryHash = ComputeSha256Hash(query.Trim().ToLowerInvariant());
        return $"ai:qa:{gameId}:{queryHash}";
    }

    public string GenerateExplainCacheKey(string gameId, string topic)
    {
        var topicHash = ComputeSha256Hash(topic.Trim().ToLowerInvariant());
        return $"ai:explain:{gameId}:{topicHash}";
    }

    public string GenerateSetupCacheKey(string gameId)
    {
        // Setup doesn't have variable parameters, so key is just the game
        return $"ai:setup:{gameId}";
    }

    public async Task InvalidateGameAsync(string gameId, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        var invalidateTasks = new List<Task>
        {
            InvalidateByPatternAsync($"ai:qa:{gameId}:*", ct),
            InvalidateByPatternAsync($"ai:explain:{gameId}:*", ct),
            InvalidateByPatternAsync($"ai:setup:{gameId}*", ct)
        };

        await Task.WhenAll(invalidateTasks);
    }

    public Task InvalidateEndpointAsync(string gameId, AiCacheEndpoint endpoint, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        var pattern = endpoint switch
        {
            AiCacheEndpoint.Qa => $"ai:qa:{gameId}:*",
            AiCacheEndpoint.Explain => $"ai:explain:{gameId}:*",
            AiCacheEndpoint.Setup => $"ai:setup:{gameId}*",
            _ => $"ai:*:{gameId}:*"
        };

        return InvalidateByPatternAsync(pattern, ct);
    }

    private async Task InvalidateByPatternAsync(string pattern, CancellationToken ct)
    {
        try
        {
            ct.ThrowIfCancellationRequested();
            var db = _redis.GetDatabase();
            var result = await db.ScriptEvaluateAsync(
                DeleteByPatternScript,
                Array.Empty<RedisKey>(),
                new RedisValue[] { pattern });

            if (result.IsNull)
            {
                _logger.LogDebug("Cache invalidation ran for pattern {Pattern} with no keys removed", pattern);
                return;
            }

            var removed = (long)result;
            _logger.LogInformation("Invalidated {RemovedCount} cache entries matching {Pattern}", removed, pattern);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Cache invalidation failed for pattern {Pattern}", pattern);
        }
    }

    private static string ComputeSha256Hash(string input)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
