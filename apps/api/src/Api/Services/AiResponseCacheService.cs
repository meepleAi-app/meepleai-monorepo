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

    private static string ComputeSha256Hash(string input)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
