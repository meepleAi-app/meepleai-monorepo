using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;

namespace Api.Services;

/// <summary>
/// AI-05: Redis-based cache for AI responses
/// Caches QA, Explain, and Setup responses to reduce latency for popular queries.
/// PERF-03: Enhanced with tag-based invalidation and statistics tracking.
/// </summary>
public class AiResponseCacheService : IAiResponseCacheService
{
    private readonly IConnectionMultiplexer _redis;
    private readonly MeepleAiDbContext _dbContext;
    private readonly ILogger<AiResponseCacheService> _logger;
    private readonly IConfiguration _configuration;

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
        MeepleAiDbContext dbContext,
        ILogger<AiResponseCacheService> logger,
        IConfiguration configuration)
    {
        _redis = redis;
        _dbContext = dbContext;
        _logger = logger;
        _configuration = configuration;
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

            // Handle both direct integer results and array results from Redis
            long removed = 0;
            if (result.Type == ResultType.Integer)
            {
                removed = (long)result;
            }
            else if (!result.IsNull)
            {
                // Try to convert to integer for other result types
                try
                {
                    removed = (long)result;
                }
                catch (InvalidCastException)
                {
                    _logger.LogDebug("Cache invalidation completed for pattern {Pattern}, but could not determine count", pattern);
                    return;
                }
            }

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

    // PERF-03: Tag-based cache invalidation methods

    public async Task InvalidateByCacheTagAsync(string tag, CancellationToken ct = default)
    {
        try
        {
            ct.ThrowIfCancellationRequested();

            var db = _redis.GetDatabase();
            var tagKey = $"tag:{tag}";

            // Get all cache keys associated with this tag
            var cacheKeys = await db.SetMembersAsync(tagKey);

            if (cacheKeys.Length == 0)
            {
                _logger.LogDebug("No cache entries found for tag: {Tag}", tag);
                return;
            }

            // Delete each cache key and its metadata
            foreach (var cacheKey in cacheKeys)
            {
                await db.KeyDeleteAsync(cacheKey);
                await db.KeyDeleteAsync($"{cacheKey}:meta");
            }

            // Delete the tag index itself
            await db.KeyDeleteAsync(tagKey);

            _logger.LogInformation("Invalidated {Count} cache entries for tag: {Tag}", cacheKeys.Length, tag);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to invalidate cache by tag: {Tag}", tag);
            // Fail gracefully - cache invalidation failures shouldn't break the app
        }
    }

    public async Task<CacheStats> GetCacheStatsAsync(string? gameId = null, CancellationToken ct = default)
    {
        try
        {
            ct.ThrowIfCancellationRequested();

            // Get stats from database
            var query = _dbContext.CacheStats.AsQueryable();

            if (!string.IsNullOrEmpty(gameId))
            {
                query = query.Where(s => s.GameId == gameId);
            }

            var dbStats = await query.ToListAsync(ct);

            var totalHits = dbStats.Sum(s => s.HitCount);
            var totalMisses = dbStats.Sum(s => s.MissCount);
            var totalRequests = totalHits + totalMisses;
            var hitRate = totalRequests > 0 ? (double)totalHits / totalRequests : 0.0;

            // Get top 10 questions by hit count
            var topQuestions = dbStats
                .OrderByDescending(s => s.HitCount)
                .Take(10)
                .Select(s => new TopQuestion
                {
                    QuestionHash = s.QuestionHash,
                    HitCount = s.HitCount,
                    MissCount = s.MissCount,
                    LastHitAt = s.LastHitAt
                })
                .ToList();

            // Calculate cache size from Redis
            long cacheSize = 0;
            int totalKeys = 0;

            try
            {
                var db = _redis.GetDatabase();
                var endpoints = _redis.GetEndPoints();

                if (endpoints.Length > 0)
                {
                    var server = _redis.GetServer(endpoints[0]);
                    var pattern = gameId != null ? $"ai:*:{gameId}:*" : "ai:*";
                    var keys = server.Keys(pattern: pattern);

                    foreach (var key in keys)
                    {
                        totalKeys++;
                        var value = await db.StringGetAsync(key);
                        if (value.HasValue)
                        {
                            cacheSize += value.ToString().Length;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to calculate cache size from Redis");
                // Continue with database stats even if Redis fails
            }

            return new CacheStats
            {
                TotalHits = totalHits,
                TotalMisses = totalMisses,
                HitRate = hitRate,
                TotalKeys = totalKeys,
                CacheSizeBytes = cacheSize,
                TopQuestions = topQuestions
            };
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get cache stats");
            // Return empty stats on error
            return new CacheStats
            {
                TotalHits = 0,
                TotalMisses = 0,
                HitRate = 0.0,
                TotalKeys = 0,
                CacheSizeBytes = 0,
                TopQuestions = new List<TopQuestion>()
            };
        }
    }

    public async Task RecordCacheAccessAsync(string gameId, string questionHash, bool isHit, CancellationToken ct = default)
    {
        try
        {
            ct.ThrowIfCancellationRequested();

            var stat = await _dbContext.CacheStats
                .FirstOrDefaultAsync(s => s.GameId == gameId && s.QuestionHash == questionHash, ct);

            if (stat == null)
            {
                // Create new stat entry
                stat = new CacheStatEntity
                {
                    GameId = gameId,
                    QuestionHash = questionHash,
                    HitCount = isHit ? 1 : 0,
                    MissCount = isHit ? 0 : 1,
                    CreatedAt = DateTime.UtcNow,
                    LastHitAt = DateTime.UtcNow
                };
                _dbContext.CacheStats.Add(stat);
            }
            else
            {
                // Update existing stat
                if (isHit)
                {
                    stat.HitCount++;
                    stat.LastHitAt = DateTime.UtcNow;
                }
                else
                {
                    stat.MissCount++;
                }
            }

            await _dbContext.SaveChangesAsync(ct);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to record cache access for game {GameId}, question {QuestionHash}", gameId, questionHash);
            // Fail gracefully - stats tracking failures shouldn't break the app
        }
    }
}
