// =============================================================================
// MeepleAI - RAG Plugin System
// Issue #3419 - Cache Plugins
// =============================================================================

using System.Collections.Concurrent;
using System.Text.Json;
using Api.BoundedContexts.KnowledgeBase.Domain.Plugins.Base;
using Api.BoundedContexts.KnowledgeBase.Domain.Plugins.Contracts;
using Api.BoundedContexts.KnowledgeBase.Domain.Plugins.Enums;
using Api.BoundedContexts.KnowledgeBase.Domain.Plugins.Models;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.KnowledgeBase.Domain.Plugins.Implementations.Cache;

/// <summary>
/// Exact query match caching plugin.
/// Fast lookup for identical (normalized) queries.
/// </summary>
[RagPlugin("cache-exact-v1",
    Category = PluginCategory.Cache,
    Name = "Exact Cache",
    Description = "Exact query match caching with optional normalization",
    Author = "MeepleAI",
    Priority = 50)]
public sealed class CacheExactPlugin : RagPluginBase
{
    /// <inheritdoc />
    public override string Id => "cache-exact-v1";

    /// <inheritdoc />
    public override string Name => "Exact Cache";

    /// <inheritdoc />
    public override string Version => "1.0.0";

    /// <inheritdoc />
    public override PluginCategory Category => PluginCategory.Cache;

    /// <inheritdoc />
    protected override string Description => "Exact query match caching with optional normalization";

    /// <inheritdoc />
    protected override IReadOnlyList<string> Tags => ["cache", "exact", "fast", "lookup"];

    /// <inheritdoc />
    protected override IReadOnlyList<string> Capabilities => ["exact-matching", "query-normalization", "ttl-management"];

    // Thread-safe in-memory cache (production uses Redis)
    private static readonly ConcurrentDictionary<string, ExactCacheEntry> _cache = new(StringComparer.Ordinal);

    public CacheExactPlugin(ILogger<CacheExactPlugin> logger) : base(logger)
    {
    }

    /// <inheritdoc />
    protected override Task<PluginOutput> ExecuteCoreAsync(
        PluginInput input,
        PluginConfig config,
        CancellationToken cancellationToken)
    {
        var query = GetQueryFromPayload(input.Payload);
        if (string.IsNullOrWhiteSpace(query))
        {
            return Task.FromResult(PluginOutput.Failed(input.ExecutionId, "Query is required in payload", "MISSING_QUERY"));
        }

        var customConfig = ParseCustomConfig(config);

        // Normalize query if configured
        var cacheKey = customConfig.ShouldNormalizeQuery ? NormalizeQuery(query) : query;

        // Try to find in cache
        if (_cache.TryGetValue(cacheKey, out var entry))
        {
            // Check TTL
            if ((DateTimeOffset.UtcNow - entry.CreatedAt).TotalSeconds <= customConfig.TtlSeconds)
            {
                var hitResult = JsonDocument.Parse(JsonSerializer.Serialize(new
                {
                    cacheHit = true,
                    cachedResponse = entry.Response
                }));

                Logger.LogInformation("Exact cache HIT: Query={Query}", query.Length > 50 ? query[..50] + "..." : query);

                return Task.FromResult(new PluginOutput
                {
                    ExecutionId = input.ExecutionId,
                    Success = true,
                    Result = hitResult,
                    Confidence = 1.0, // Exact match = perfect confidence
                    Metrics = new PluginExecutionMetrics { CacheHit = true }
                });
            }
            else
            {
                // Entry expired, remove it and dispose
                if (_cache.TryRemove(cacheKey, out var expiredEntry))
                {
                    expiredEntry.Response.Dispose();
                }
            }
        }

        var missResult = JsonDocument.Parse(JsonSerializer.Serialize(new
        {
            cacheHit = false,
            cachedResponse = (object?)null
        }));

        Logger.LogDebug("Exact cache MISS: Query={Query}", query.Length > 50 ? query[..50] + "..." : query);

        return Task.FromResult(new PluginOutput
        {
            ExecutionId = input.ExecutionId,
            Success = true,
            Result = missResult,
            Metrics = new PluginExecutionMetrics { CacheHit = false }
        });
    }

    private static string NormalizeQuery(string query)
    {
        // Normalize: lowercase, trim, collapse whitespace
        var normalized = query.ToLowerInvariant().Trim();
        normalized = System.Text.RegularExpressions.Regex.Replace(normalized, @"\s+", " ", System.Text.RegularExpressions.RegexOptions.None, TimeSpan.FromSeconds(1));
        return normalized;
    }

    /// <summary>
    /// Stores a response in the exact match cache.
    /// The JsonDocument is cloned to ensure the cache owns its copy.
    /// </summary>
    public static void StoreInCache(string query, JsonDocument response, bool normalize = true)
    {
        var cacheKey = normalize ? NormalizeQuery(query) : query;
        // Clone the JsonDocument to ensure the cache owns its copy
        var clonedResponse = JsonDocument.Parse(response.RootElement.GetRawText());

        // If replacing an existing entry, dispose the old one
        if (_cache.TryGetValue(cacheKey, out var existingEntry))
        {
            existingEntry.Response.Dispose();
        }

        _cache[cacheKey] = new ExactCacheEntry(clonedResponse, DateTimeOffset.UtcNow);
    }

    /// <summary>
    /// Clears expired entries from the cache and disposes their JsonDocuments.
    /// </summary>
    public static void CleanupExpiredEntries(int ttlSeconds)
    {
        var now = DateTimeOffset.UtcNow;
        var keysToRemove = _cache
            .Where(kvp => (now - kvp.Value.CreatedAt).TotalSeconds > ttlSeconds)
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var key in keysToRemove)
        {
            if (_cache.TryRemove(key, out var entry))
            {
                entry.Response.Dispose();
            }
        }
    }

    /// <summary>
    /// Gets cache statistics.
    /// </summary>
    public static CacheStats GetStats()
    {
        return new CacheStats(_cache.Count, 0, 0); // Hit/miss tracking would require additional state
    }

    /// <summary>
    /// Clears all entries from the cache and disposes their JsonDocuments.
    /// </summary>
    public static void ClearCache()
    {
        var entries = _cache.ToArray();
        _cache.Clear();

        foreach (var kvp in entries)
        {
            kvp.Value.Response.Dispose();
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

    private static ExactCacheConfig ParseCustomConfig(PluginConfig config)
    {
        if (config.CustomConfig == null)
        {
            return new ExactCacheConfig();
        }

        var root = config.CustomConfig.RootElement;
        return new ExactCacheConfig
        {
            ShouldNormalizeQuery = !root.TryGetProperty("normalizeQuery", out var n) || n.GetBoolean(),
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
            "description": "Input for exact cache plugin",
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
            "description": "Output from exact cache plugin",
            "properties": {
                "cacheHit": {
                    "type": "boolean",
                    "description": "Whether a cache hit occurred"
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
            "description": "Configuration for exact cache plugin",
            "properties": {
                "normalizeQuery": {
                    "type": "boolean",
                    "description": "Normalize query (lowercase, trim, collapse whitespace)",
                    "default": true
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

    private sealed record ExactCacheEntry(JsonDocument Response, DateTimeOffset CreatedAt);

    private sealed class ExactCacheConfig
    {
        public bool ShouldNormalizeQuery { get; init; }
        public int TtlSeconds { get; init; } = 3600;
    }

    public sealed record CacheStats(int EntryCount, long TotalHits, long TotalMisses);
}
