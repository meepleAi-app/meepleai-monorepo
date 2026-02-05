// =============================================================================
// MeepleAI - RAG Plugin System
// Issue #3424 - Transform/Filter Plugins
// =============================================================================

using System.Text.Json;
using Api.BoundedContexts.KnowledgeBase.Domain.Plugins.Base;
using Api.BoundedContexts.KnowledgeBase.Domain.Plugins.Contracts;
using Api.BoundedContexts.KnowledgeBase.Domain.Plugins.Enums;
using Api.BoundedContexts.KnowledgeBase.Domain.Plugins.Models;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.KnowledgeBase.Domain.Plugins.Implementations.Filter;

/// <summary>
/// Deduplication filter plugin for removing duplicate or near-duplicate documents.
/// Supports exact match, semantic similarity, and content-hash deduplication.
/// </summary>
[RagPlugin("filter-deduplicator-v1",
    Category = PluginCategory.Filter,
    Name = "Deduplicator",
    Description = "Removes duplicate or near-duplicate documents",
    Author = "MeepleAI")]
public sealed class FilterDeduplicatorPlugin : RagPluginBase
{
    /// <inheritdoc />
    public override string Id => "filter-deduplicator-v1";

    /// <inheritdoc />
    public override string Name => "Deduplicator";

    /// <inheritdoc />
    public override string Version => "1.0.0";

    /// <inheritdoc />
    public override PluginCategory Category => PluginCategory.Filter;

    /// <inheritdoc />
    protected override string Description => "Removes duplicate or near-duplicate documents";

    /// <inheritdoc />
    protected override IReadOnlyList<string> Tags => ["filter", "deduplicator", "unique", "duplicates"];

    /// <inheritdoc />
    protected override IReadOnlyList<string> Capabilities => ["exact-match", "semantic-similarity", "content-hash"];

    public FilterDeduplicatorPlugin(ILogger<FilterDeduplicatorPlugin> logger) : base(logger)
    {
    }

    /// <inheritdoc />
    protected override async Task<PluginOutput> ExecuteCoreAsync(
        PluginInput input,
        PluginConfig config,
        CancellationToken cancellationToken)
    {
        var documents = ParseDocuments(input.Payload);

        if (documents.Count == 0)
        {
            var emptyResult = JsonDocument.Parse("""{"documents": [], "removedCount": 0}""");
            return PluginOutput.Successful(input.ExecutionId, emptyResult);
        }

        var customConfig = ParseCustomConfig(config);

        var deduplicated = customConfig.Method switch
        {
            "semantic" => await DeduplicateSemanticAsync(documents, customConfig, cancellationToken).ConfigureAwait(false),
            "hash" => DeduplicateByHash(documents, customConfig),
            _ => DeduplicateExact(documents, customConfig)
        };

        var removedCount = documents.Count - deduplicated.Count;

        var result = JsonDocument.Parse(JsonSerializer.Serialize(new
        {
            documents = deduplicated.Select(d => new
            {
                id = d.Id,
                content = d.Content,
                score = d.Score,
                source = d.Source
            }),
            removedCount = removedCount,
            method = customConfig.Method
        }));

        Logger.LogInformation(
            "Deduplication: Method={Method}, Input={Input}, Output={Output}, Removed={Removed}",
            customConfig.Method, documents.Count, deduplicated.Count, removedCount);

        return PluginOutput.Successful(input.ExecutionId, result);
    }

    private static List<DocumentItem> DeduplicateExact(List<DocumentItem> documents, DeduplicatorConfig config)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var unique = new List<DocumentItem>();

        foreach (var doc in documents.OrderByDescending(d => d.Score))
        {
            var key = string.Equals(config.Field, "content", StringComparison.Ordinal)
                ? doc.Content.Trim()
                : doc.Id;

            if (seen.Add(key))
            {
                unique.Add(doc);
            }
        }

        return unique;
    }

    private static List<DocumentItem> DeduplicateByHash(List<DocumentItem> documents, DeduplicatorConfig config)
    {
        var seen = new HashSet<int>();
        var unique = new List<DocumentItem>();

        foreach (var doc in documents.OrderByDescending(d => d.Score))
        {
            // Simple hash-based deduplication (production uses xxHash or similar)
            var hash = ComputeSimpleHash(doc.Content, config.NormalizeContent);

            if (seen.Add(hash))
            {
                unique.Add(doc);
            }
        }

        return unique;
    }

    private static async Task<List<DocumentItem>> DeduplicateSemanticAsync(
        List<DocumentItem> documents,
        DeduplicatorConfig config,
        CancellationToken cancellationToken)
    {
        // Simulate semantic similarity computation
        await Task.Delay(20, cancellationToken).ConfigureAwait(false);

        var unique = new List<DocumentItem>();
        var processed = new List<DocumentItem>();

        foreach (var doc in documents.OrderByDescending(d => d.Score))
        {
            var isDuplicate = false;

            foreach (var existing in processed)
            {
                var similarity = ComputeSimpleSimilarity(doc.Content, existing.Content);

                if (similarity >= config.SimilarityThreshold)
                {
                    isDuplicate = true;
                    break;
                }
            }

            if (!isDuplicate)
            {
                unique.Add(doc);
                processed.Add(doc);
            }
        }

        return unique;
    }

    private static int ComputeSimpleHash(string content, bool normalize)
    {
        if (normalize)
        {
            content = content.ToLowerInvariant()
                .Replace(" ", "", StringComparison.Ordinal)
                .Replace("\n", "", StringComparison.Ordinal)
                .Replace("\r", "", StringComparison.Ordinal)
                .Replace("\t", "", StringComparison.Ordinal);
        }

        return content.GetHashCode(StringComparison.Ordinal);
    }

    private static double ComputeSimpleSimilarity(string a, string b)
    {
        // Simple Jaccard similarity for demonstration
        var wordsA = a.ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries).ToHashSet(StringComparer.Ordinal);
        var wordsB = b.ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries).ToHashSet(StringComparer.Ordinal);

        if (wordsA.Count == 0 || wordsB.Count == 0)
        {
            return 0;
        }

        var intersection = wordsA.Intersect(wordsB, StringComparer.Ordinal).Count();
        var union = wordsA.Union(wordsB, StringComparer.Ordinal).Count();

        return (double)intersection / union;
    }

    private static List<DocumentItem> ParseDocuments(JsonDocument payload)
    {
        var documents = new List<DocumentItem>();

        if (payload.RootElement.TryGetProperty("documents", out var docsElement) &&
            docsElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var doc in docsElement.EnumerateArray())
            {
                var id = doc.TryGetProperty("id", out var i) ? i.GetString() ?? Guid.NewGuid().ToString() : Guid.NewGuid().ToString();
                var content = doc.TryGetProperty("content", out var c) ? c.GetString() ?? "" : "";
                var score = doc.TryGetProperty("score", out var s) ? s.GetDouble() : 1.0;
                var source = doc.TryGetProperty("source", out var src) ? src.GetString() ?? "unknown" : "unknown";
                documents.Add(new DocumentItem(id, content, score, source));
            }
        }

        return documents;
    }

    private static DeduplicatorConfig ParseCustomConfig(PluginConfig config)
    {
        if (config.CustomConfig == null)
        {
            return new DeduplicatorConfig();
        }

        var root = config.CustomConfig.RootElement;
        return new DeduplicatorConfig
        {
            Method = root.TryGetProperty("method", out var m) ? m.GetString() ?? "exact" : "exact",
            Field = root.TryGetProperty("field", out var f) ? f.GetString() ?? "content" : "content",
            SimilarityThreshold = root.TryGetProperty("similarityThreshold", out var st) ? st.GetDouble() : 0.85,
            NormalizeContent = !root.TryGetProperty("normalizeContent", out var nc) || nc.GetBoolean()
        };
    }

    /// <inheritdoc />
    protected override JsonDocument CreateInputSchema()
    {
        return CreateSchemaFromJson("""
        {
            "$schema": "https://json-schema.org/draft/2020-12/schema",
            "type": "object",
            "properties": {
                "documents": {
                    "type": "array",
                    "items": {
                        "type": "object",
                        "properties": {
                            "id": { "type": "string" },
                            "content": { "type": "string" },
                            "score": { "type": "number" },
                            "source": { "type": "string" }
                        }
                    }
                }
            },
            "required": ["documents"]
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
            "properties": {
                "documents": { "type": "array" },
                "removedCount": { "type": "integer" },
                "method": { "type": "string" }
            },
            "required": ["documents", "removedCount"]
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
            "properties": {
                "method": {
                    "type": "string",
                    "enum": ["exact", "hash", "semantic"],
                    "default": "exact"
                },
                "field": {
                    "type": "string",
                    "enum": ["id", "content"],
                    "default": "content"
                },
                "similarityThreshold": {
                    "type": "number",
                    "minimum": 0,
                    "maximum": 1,
                    "default": 0.85
                },
                "normalizeContent": {
                    "type": "boolean",
                    "default": true
                }
            }
        }
        """);
    }

    private sealed record DocumentItem(string Id, string Content, double Score, string Source);

    private sealed class DeduplicatorConfig
    {
        public string Method { get; init; } = "exact";
        public string Field { get; init; } = "content";
        public double SimilarityThreshold { get; init; } = 0.85;
        public bool NormalizeContent { get; init; } = true;
    }
}
