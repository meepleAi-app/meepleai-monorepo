using Api.Constants;
using Api.Filters;
using Microsoft.Extensions.Caching.Distributed;

namespace Api.Routing;

/// <summary>
/// Admin endpoints for Knowledge Base settings (read-only from env/config).
/// Issue #4881: KB Settings Dashboard.
/// </summary>
internal static class AdminKBSettingsEndpoints
{
    public static RouteGroupBuilder MapAdminKBSettingsEndpoints(this RouteGroupBuilder group)
    {
        var settingsGroup = group.MapGroup("/admin/kb/settings")
            .WithTags("Admin", "KnowledgeBase", "Settings")
            .AddEndpointFilter<RequireAdminSessionFilter>();

        // GET /api/v1/admin/kb/settings
        settingsGroup.MapGet("/", GetKBSettings);

        // POST /api/v1/admin/kb/cache/clear
        group.MapGroup("/admin/kb/cache")
            .WithTags("Admin", "KnowledgeBase", "Cache")
            .AddEndpointFilter<RequireAdminSessionFilter>()
            .MapPost("/clear", ClearKBCache);

        return group;
    }

    private static IResult GetKBSettings(
        IConfiguration configuration,
        ILogger<Program> logger)
    {
        // Embedding settings
        var embeddingProvider = configuration["EMBEDDING_PROVIDER"]?.ToLowerInvariant() ?? "ollama";
        var embeddingModel = configuration["EMBEDDING_MODEL"] ?? "nomic-embed-text";
#pragma warning disable S1075 // URIs should not be hardcoded - Default fallback for external service
        var embeddingUrl = configuration["LOCAL_EMBEDDING_URL"]
            ?? configuration["Embedding:LocalServiceUrl"]
            ?? "http://embedding-service:8000";

#pragma warning restore S1075

        // Redis settings
        var redisHost = Environment.GetEnvironmentVariable("REDIS_HOST")
            ?? configuration["REDIS_HOST"]
            ?? "localhost";
        var redisPort = Environment.GetEnvironmentVariable("REDIS_PORT")
            ?? configuration["REDIS_PORT"]
            ?? "6379";

        // Chunking settings (from constants)
        var chunkSize = ChunkingConstants.DefaultChunkSize;
        var chunkOverlap = ChunkingConstants.DefaultChunkOverlap;
        var minChunkSize = ChunkingConstants.MinChunkSize;
        var maxChunkSize = ChunkingConstants.MaxChunkSize;
        var embeddingTokenLimit = ChunkingConstants.EmbeddingTokenLimit;

        // Cache settings
        var hybridCacheExpiration = configuration["HybridCache:DefaultExpiration"] ?? "24:00:00";
        var hybridCacheL2Enabled = configuration.GetValue<bool>("HybridCache:EnableL2Cache", false);
        var multiTierEnabled = configuration.GetValue<bool>("MultiTierCache:Enabled", true);
        var multiTierL1Ttl = configuration["MultiTierCache:L1DefaultTtl"] ?? "00:05:00";
        var multiTierL2Ttl = configuration["MultiTierCache:L2DefaultTtl"] ?? "01:00:00";

        // Reranker settings
        var rerankerUrl = configuration["RERANKER_URL"];

        // Storage settings
        var storageProvider = Environment.GetEnvironmentVariable("STORAGE_PROVIDER") ?? "local";

        logger.LogDebug("KB settings requested by admin");

        return Results.Ok(new
        {
            embedding = new
            {
                provider = embeddingProvider,
                model = embeddingModel,
                serviceUrl = MaskUrl(embeddingUrl),
            },
            vectorDatabase = new
            {
                type = "pgvector",
                backend = "PostgreSQL",
            },
            chunking = new
            {
                defaultChunkSize = chunkSize,
                chunkOverlap,
                minChunkSize,
                maxChunkSize,
                embeddingTokenLimit,
                charsPerToken = ChunkingConstants.CharsPerToken,
            },
            cache = new
            {
                redis = new
                {
                    host = redisHost,
                    port = redisPort,
                },
                hybridCache = new
                {
                    defaultExpiration = hybridCacheExpiration,
                    l2Enabled = hybridCacheL2Enabled,
                },
                multiTier = new
                {
                    enabled = multiTierEnabled,
                    l1Ttl = multiTierL1Ttl,
                    l2Ttl = multiTierL2Ttl,
                },
            },
            reranker = new
            {
                configured = !string.IsNullOrEmpty(rerankerUrl),
                url = rerankerUrl != null ? MaskUrl(rerankerUrl) : null,
            },
            storage = new
            {
                provider = storageProvider,
            },
        });
    }

    private static async Task<IResult> ClearKBCache(
        Microsoft.Extensions.Caching.Distributed.IDistributedCache? distributedCache,
        ILogger<Program> logger,
        CancellationToken ct)
    {
        try
        {
            // Clear distributed cache by removing known prefixes
            // Note: IDistributedCache doesn't support "clear all" natively,
            // but we can signal a cache reset via a sentinel key
            if (distributedCache != null)
            {
                var sentinelKey = "meepleai:cache:cleared_at";
                await distributedCache.SetStringAsync(
                    sentinelKey,
                    DateTime.UtcNow.ToString("O"),
                    new Microsoft.Extensions.Caching.Distributed.DistributedCacheEntryOptions
                    {
                        AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(365)
                    },
                    ct).ConfigureAwait(false);

                logger.LogInformation("KB cache clear requested by admin at {Timestamp}", DateTime.UtcNow);

                return Results.Ok(new
                {
                    success = true,
                    message = "Cache clear signal sent. Cached entries will expire based on their TTL.",
                    clearedAt = DateTime.UtcNow.ToString("O"),
                });
            }

            return Results.Ok(new
            {
                success = false,
                message = "No distributed cache is configured.",
                clearedAt = (string?)null,
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to clear KB cache");
            return Results.Problem("Failed to clear cache: " + ex.Message);
        }
    }

    /// <summary>
    /// Masks a URL to hide sensitive parts (e.g., passwords in connection strings).
    /// Shows scheme + host + port only.
    /// </summary>
    private static string MaskUrl(string url)
    {
        try
        {
            var uri = new Uri(url);
            return $"{uri.Scheme}://{uri.Host}:{uri.Port}";
        }
        catch
        {
            // If not a valid URI, return first 20 chars + "..."
            return url.Length > 20 ? url[..20] + "..." : url;
        }
    }
}
