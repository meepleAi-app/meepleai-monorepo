using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;

namespace Api.Infrastructure.Translation;

internal sealed class RedisTranslationCache(
    IDistributedCache distributedCache,
    ILogger<RedisTranslationCache> logger) : ITranslationCache
{
    private static readonly TimeSpan DefaultNarrativeTtl = TimeSpan.FromDays(7);
    private const string KeyPrefix = "meepleai:translation:";

    public async Task<TranslationResult?> TryGetAsync(
        Guid gameId, string sourceText, string sourceLanguage, string targetLanguage, CancellationToken ct = default)
    {
        var key = BuildKey(gameId, sourceText, sourceLanguage, targetLanguage);
        try
        {
            var bytes = await distributedCache.GetAsync(key, ct).ConfigureAwait(false);
            if (bytes is null or { Length: 0 }) return null;
            return JsonSerializer.Deserialize<TranslationResult>(bytes);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "[TranslationCache] Cache read failed for key {Key}", key);
            return null;
        }
    }

    public async Task SetAsync(
        Guid gameId, string sourceText, string sourceLanguage, string targetLanguage,
        TranslationResult result, TimeSpan? ttl = null, CancellationToken ct = default)
    {
        var key = BuildKey(gameId, sourceText, sourceLanguage, targetLanguage);
        var options = new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = ttl ?? DefaultNarrativeTtl
        };
        try
        {
            var bytes = JsonSerializer.SerializeToUtf8Bytes(result);
            await distributedCache.SetAsync(key, bytes, options, ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "[TranslationCache] Cache write failed for key {Key}", key);
        }
    }

    public Task InvalidateGameAsync(Guid gameId, CancellationToken ct = default)
    {
        // Phase 2: TTL is primary guard. Pattern delete requires IConnectionMultiplexer (Phase 3).
        // R-15 mitigation: log + no-op for now.
        logger.LogInformation(
            "[TranslationCache] InvalidateGameAsync called for game {GameId} — TTL-based expiry is primary guard",
            gameId);
        return Task.CompletedTask;
    }

    private static string BuildKey(Guid gameId, string text, string src, string tgt)
    {
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
        return $"{KeyPrefix}{gameId}:{src}:{tgt}:{hash[..16]}";
    }
}
