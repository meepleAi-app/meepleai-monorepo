// =============================================================================
// MeepleAI - RAG Plugin System
// Issue #3419 - Cache Plugins
// =============================================================================

using System.Text.Json;
using Api.BoundedContexts.KnowledgeBase.Domain.Plugins.Base;
using Api.BoundedContexts.KnowledgeBase.Domain.Plugins.Contracts;
using Api.BoundedContexts.KnowledgeBase.Domain.Plugins.Enums;
using Api.BoundedContexts.KnowledgeBase.Domain.Plugins.Models;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.KnowledgeBase.Domain.Plugins.Implementations.Cache;

/// <summary>
/// Semantic similarity caching plugin using embeddings.
/// Finds cached responses for semantically similar queries.
/// </summary>
[RagPlugin("cache-semantic-v1",
    Category = PluginCategory.Cache,
    Name = "Semantic Cache",
    Description = "Semantic similarity caching using embeddings for intelligent cache matching",
    Author = "MeepleAI")]
public sealed class CacheSemanticPlugin : RagPluginBase
{
    /// <inheritdoc />
    public override string Id => "cache-semantic-v1";

    /// <inheritdoc />
    public override string Name => "Semantic Cache";

    /// <inheritdoc />
    public override string Version => "1.0.0";

    /// <inheritdoc />
    public override PluginCategory Category => PluginCategory.Cache;

    /// <inheritdoc />
    protected override string Description => "Semantic similarity caching using embeddings";

    /// <inheritdoc />
    protected override IReadOnlyList<string> Tags => ["cache", "semantic", "embeddings", "similarity"];

    /// <inheritdoc />
    protected override IReadOnlyList<string> Capabilities => ["semantic-matching", "embedding-search", "ttl-management"];

    // In-memory cache for demonstration (production uses Redis + Qdrant)
    private static readonly Dictionary<string, CacheEntry> _cache = new(StringComparer.Ordinal);
    private static readonly System.Threading.Lock _cacheLock = new();

    public CacheSemanticPlugin(ILogger<CacheSemanticPlugin> logger) : base(logger)
    {
    }

    /// <inheritdoc />
    protected override async Task<PluginOutput> ExecuteCoreAsync(
        PluginInput input,
        PluginConfig config,
        CancellationToken cancellationToken)
    {
        var query = GetQueryFromPayload(input.Payload);
        if (string.IsNullOrWhiteSpace(query))
        {
            return PluginOutput.Failed(input.ExecutionId, "Query is required in payload", "MISSING_QUERY");
        }

        var customConfig = ParseCustomConfig(config);

        // Generate embedding for the query (simulated)
        var queryEmbedding = await GenerateEmbeddingAsync(query, cancellationToken).ConfigureAwait(false);

        // Search for similar cached entries
        var cacheResult = SearchCache(queryEmbedding, customConfig);

        JsonDocument result;
        if (cacheResult.CacheHit)
        {
            result = JsonDocument.Parse(JsonSerializer.Serialize(new
            {
                cacheHit = true,
                similarity = cacheResult.Similarity,
                cachedResponse = cacheResult.CachedResponse
            }));

            Logger.LogInformation(
                "Semantic cache HIT: Similarity={Similarity:F3}, Query={Query}",
                cacheResult.Similarity, query.Length > 50 ? query[..50] + "..." : query);

            return new PluginOutput
            {
                ExecutionId = input.ExecutionId,
                Success = true,
                Result = result,
                Confidence = cacheResult.Similarity,
                Metrics = new PluginExecutionMetrics { CacheHit = true }
            };
        }

        result = JsonDocument.Parse(JsonSerializer.Serialize(new
        {
            cacheHit = false,
            similarity = (double?)null,
            cachedResponse = (object?)null
        }));

        Logger.LogDebug("Semantic cache MISS: Query={Query}", query.Length > 50 ? query[..50] + "..." : query);

        return new PluginOutput
        {
            ExecutionId = input.ExecutionId,
            Success = true,
            Result = result,
            Metrics = new PluginExecutionMetrics { CacheHit = false }
        };
    }

    private static async Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken)
    {
        // Simulate embedding generation (production calls embedding service)
        await Task.Delay(5, cancellationToken).ConfigureAwait(false);

        // Create a simple hash-based pseudo-embedding for demonstration
        var embedding = new float[384];
        var hash = text.GetHashCode(StringComparison.Ordinal);
        var random = new Random(hash);

        for (int i = 0; i < embedding.Length; i++)
        {
            embedding[i] = (float)(random.NextDouble() * 2 - 1);
        }

        // Normalize
        var magnitude = (float)Math.Sqrt(embedding.Sum(x => x * x));
        for (int i = 0; i < embedding.Length; i++)
        {
            embedding[i] /= magnitude;
        }

        return embedding;
    }

    private static CacheSearchResult SearchCache(float[] queryEmbedding, SemanticCacheConfig config)
    {
        lock (_cacheLock)
        {
            var now = DateTimeOffset.UtcNow;
            float bestSimilarity = 0;
            CacheEntry? bestEntry = null;

            foreach (var (_, entry) in _cache)
            {
                // Check TTL
                if ((now - entry.CreatedAt).TotalSeconds > config.TtlSeconds)
                {
                    continue;
                }

                var similarity = CosineSimilarity(queryEmbedding, entry.Embedding);
                if (similarity >= config.SimilarityThreshold && similarity > bestSimilarity)
                {
                    bestSimilarity = similarity;
                    bestEntry = entry;
                }
            }

            if (bestEntry != null)
            {
                return new CacheSearchResult(true, bestSimilarity, bestEntry.Response);
            }

            return new CacheSearchResult(false, 0, null);
        }
    }

    private static float CosineSimilarity(float[] a, float[] b)
    {
        if (a.Length != b.Length)
        {
            return 0;
        }

        float dot = 0, magA = 0, magB = 0;
        for (int i = 0; i < a.Length; i++)
        {
            dot += a[i] * b[i];
            magA += a[i] * a[i];
            magB += b[i] * b[i];
        }

        var denominator = (float)(Math.Sqrt(magA) * Math.Sqrt(magB));
        return denominator > 0 ? dot / denominator : 0;
    }

    /// <summary>
    /// Stores a response in the semantic cache.
    /// The JsonDocument is cloned to ensure the cache owns its copy.
    /// </summary>
    public static void StoreInCache(string query, float[] embedding, JsonDocument response)
    {
        lock (_cacheLock)
        {
            var key = Convert.ToBase64String(System.Security.Cryptography.SHA256.HashData(
                System.Text.Encoding.UTF8.GetBytes(query)));

            // Clone the JsonDocument to ensure the cache owns its copy
            var clonedResponse = JsonDocument.Parse(response.RootElement.GetRawText());

            // If replacing an existing entry, dispose the old one
            if (_cache.TryGetValue(key, out var existingEntry))
            {
                existingEntry.Response.Dispose();
            }

            _cache[key] = new CacheEntry(embedding, clonedResponse, DateTimeOffset.UtcNow);
        }
    }

    /// <summary>
    /// Clears expired entries from the cache and disposes their JsonDocuments.
    /// </summary>
    public static void CleanupExpiredEntries(int ttlSeconds)
    {
        lock (_cacheLock)
        {
            var now = DateTimeOffset.UtcNow;
            var keysToRemove = _cache
                .Where(kvp => (now - kvp.Value.CreatedAt).TotalSeconds > ttlSeconds)
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var key in keysToRemove)
            {
                if (_cache.TryGetValue(key, out var entry))
                {
                    entry.Response.Dispose();
                    _cache.Remove(key);
                }
            }
        }
    }

    /// <summary>
    /// Clears all entries from the cache and disposes their JsonDocuments.
    /// </summary>
    public static void ClearCache()
    {
        lock (_cacheLock)
        {
            foreach (var kvp in _cache)
            {
                kvp.Value.Response.Dispose();
            }
            _cache.Clear();
        }
    }

    private static string GetQueryFromPayload(JsonDocument payload)
    {
        if (payload.RootElement.TryGetProperty("query", out var queryElement))
        {
            return queryElement.GetString() ?? string.Empty;
        }
        return string.Empty;
    }

    private static SemanticCacheConfig ParseCustomConfig(PluginConfig config)
    {
        if (config.CustomConfig == null)
        {
            return new SemanticCacheConfig();
        }

        var root = config.CustomConfig.RootElement;
        return new SemanticCacheConfig
        {
            SimilarityThreshold = root.TryGetProperty("similarityThreshold", out var t) ? t.GetDouble() : 0.95,
            MaxCacheSize = root.TryGetProperty("maxCacheSize", out var m) ? m.GetInt32() : 10000,
            TtlSeconds = root.TryGetProperty("ttlSeconds", out var ttl) ? ttl.GetInt32() : 3600
        };
    }

    /// <inheritdoc />
    protected override ValidationResult ValidateInputCore(PluginInput input)
    {
        var errors = new List<ValidationError>();

        if (!input.Payload.RootElement.TryGetProperty("query", out var queryElement) ||
            string.IsNullOrWhiteSpace(queryElement.GetString()))
        {
            errors.Add(new ValidationError
            {
                Message = "Query is required in payload",
                PropertyPath = "payload.query",
                Code = "MISSING_QUERY"
            });
        }

        return errors.Count == 0 ? ValidationResult.Success() : ValidationResult.Failure([.. errors]);
    }

    /// <inheritdoc />
    protected override ValidationResult ValidateConfigCore(PluginConfig config)
    {
        var errors = new List<ValidationError>();

        if (config.CustomConfig != null)
        {
            var root = config.CustomConfig.RootElement;

            if (root.TryGetProperty("similarityThreshold", out var threshold))
            {
                var value = threshold.GetDouble();
                if (value < 0 || value > 1)
                {
                    errors.Add(new ValidationError
                    {
                        Message = "Similarity threshold must be between 0 and 1",
                        PropertyPath = "customConfig.similarityThreshold",
                        Code = "INVALID_THRESHOLD",
                        AttemptedValue = value
                    });
                }
            }

            if (root.TryGetProperty("ttlSeconds", out var ttl))
            {
                var value = ttl.GetInt32();
                if (value <= 0)
                {
                    errors.Add(new ValidationError
                    {
                        Message = "TTL must be greater than 0",
                        PropertyPath = "customConfig.ttlSeconds",
                        Code = "INVALID_TTL",
                        AttemptedValue = value
                    });
                }
            }
        }

        return errors.Count == 0 ? ValidationResult.Success() : ValidationResult.Failure([.. errors]);
    }

    /// <inheritdoc />
    protected override JsonDocument CreateInputSchema()
    {
        return CreateSchemaFromJson("""
        {
            "$schema": "https://json-schema.org/draft/2020-12/schema",
            "type": "object",
            "description": "Input for semantic cache plugin",
            "properties": {
                "query": {
                    "type": "string",
                    "description": "The query to search in cache"
                }
            },
            "required": ["query"]
        }
        """);
    }

    /// <inheritdoc />
    protected override JsonDocument CreateOutputSchema()
    {
        return CreateSchemaFromJson("""
        {
            "$schema": "https://json-schema.org/draft/2020-12/schema",
            "type": "object",
            "description": "Output from semantic cache plugin",
            "properties": {
                "cacheHit": {
                    "type": "boolean",
                    "description": "Whether a cache hit occurred"
                },
                "similarity": {
                    "type": ["number", "null"],
                    "minimum": 0,
                    "maximum": 1,
                    "description": "Similarity score if cache hit"
                },
                "cachedResponse": {
                    "type": ["object", "null"],
                    "description": "The cached response if hit"
                }
            },
            "required": ["cacheHit"]
        }
        """);
    }

    /// <inheritdoc />
    protected override JsonDocument CreateConfigSchema()
    {
        return CreateSchemaFromJson("""
        {
            "$schema": "https://json-schema.org/draft/2020-12/schema",
            "type": "object",
            "description": "Configuration for semantic cache plugin",
            "properties": {
                "similarityThreshold": {
                    "type": "number",
                    "minimum": 0,
                    "maximum": 1,
                    "description": "Minimum similarity for cache hit",
                    "default": 0.95
                },
                "maxCacheSize": {
                    "type": "integer",
                    "minimum": 1,
                    "description": "Maximum cache entries",
                    "default": 10000
                },
                "ttlSeconds": {
                    "type": "integer",
                    "minimum": 1,
                    "description": "Time to live in seconds",
                    "default": 3600
                }
            }
        }
        """);
    }

    private sealed record CacheEntry(float[] Embedding, JsonDocument Response, DateTimeOffset CreatedAt);
    private sealed record CacheSearchResult(bool CacheHit, double Similarity, JsonDocument? CachedResponse);

    private sealed class SemanticCacheConfig
    {
        public double SimilarityThreshold { get; init; } = 0.95;
        public int MaxCacheSize { get; init; } = 10000;
        public int TtlSeconds { get; init; } = 3600;
    }
}
