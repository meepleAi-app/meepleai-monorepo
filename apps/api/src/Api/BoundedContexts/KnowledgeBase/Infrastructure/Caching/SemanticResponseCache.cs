using System.Text.Json;
using Api.BoundedContexts.KnowledgeBase.Application.Services;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace Api.BoundedContexts.KnowledgeBase.Infrastructure.Caching;

internal sealed class SemanticResponseCache(
    IConnectionMultiplexer redis,
    ILogger<SemanticResponseCache> logger) : ISemanticResponseCache
{
    private const float SimilarityThreshold = 0.95f;
    private static readonly TimeSpan Ttl = TimeSpan.FromHours(24);

    public async Task<CachedRagResponse?> TryGetAsync(
        Guid gameId, float[] queryVector, CancellationToken ct = default)
    {
        try
        {
            var db = redis.GetDatabase();
            var server = redis.GetServer(redis.GetEndPoints()[0]);
            var keys = server.Keys(pattern: $"rag:cache:{gameId}:*").ToList();

            foreach (var key in keys)
            {
                var json = await db.StringGetAsync(key).ConfigureAwait(false);
                if (!json.HasValue) continue;

                var entry = JsonSerializer.Deserialize<CacheEntry>(json.ToString());
                if (entry == null) continue;

                var similarity = CosineSimilarity(queryVector, entry.QueryVector);
                if (similarity >= SimilarityThreshold)
                {
                    logger.LogInformation(
                        "Cache hit for game {GameId}, similarity {Sim:F3}", gameId, similarity);
                    return entry.Response;
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex, "Semantic cache lookup failed for game {GameId} — non-critical", gameId);
        }
        return null;
    }

    public async Task SetAsync(
        Guid gameId, float[] queryVector, CachedRagResponse response, CancellationToken ct = default)
    {
        try
        {
            var db = redis.GetDatabase();
            var key = $"rag:cache:{gameId}:{Guid.NewGuid():N}";
            var entry = new CacheEntry(queryVector, response);
            var json = JsonSerializer.Serialize(entry);
            await db.StringSetAsync(key, json, Ttl).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex, "Semantic cache set failed for game {GameId} — non-critical", gameId);
        }
    }

    public async Task InvalidateGameAsync(Guid gameId, CancellationToken ct = default)
    {
        try
        {
            var server = redis.GetServer(redis.GetEndPoints()[0]);
            var keys = server.Keys(pattern: $"rag:cache:{gameId}:*").ToArray();
            if (keys.Length > 0)
            {
                var db = redis.GetDatabase();
                await db.KeyDeleteAsync(keys).ConfigureAwait(false);
                logger.LogInformation(
                    "Deleted {Count} cache entries for game {GameId}", keys.Length, gameId);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex, "Cache invalidation failed for game {GameId}", gameId);
        }
    }

    private static float CosineSimilarity(float[] a, float[] b)
    {
        if (a.Length != b.Length) return 0f;
        float dot = 0, normA = 0, normB = 0;
        for (var i = 0; i < a.Length; i++)
        {
            dot += a[i] * b[i];
            normA += a[i] * a[i];
            normB += b[i] * b[i];
        }
        return normA < 1e-10f || normB < 1e-10f ? 0f : dot / (MathF.Sqrt(normA) * MathF.Sqrt(normB));
    }

    private sealed record CacheEntry(float[] QueryVector, CachedRagResponse Response);
}
