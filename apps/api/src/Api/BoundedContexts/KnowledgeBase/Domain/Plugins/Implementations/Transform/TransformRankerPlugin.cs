// =============================================================================
// MeepleAI - RAG Plugin System
// Issue #3424 - Transform/Filter Plugins
// =============================================================================

using System.Diagnostics;
using System.Text.Json;
using Api.BoundedContexts.KnowledgeBase.Domain.Plugins.Base;
using Api.BoundedContexts.KnowledgeBase.Domain.Plugins.Contracts;
using Api.BoundedContexts.KnowledgeBase.Domain.Plugins.Enums;
using Api.BoundedContexts.KnowledgeBase.Domain.Plugins.Models;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.KnowledgeBase.Domain.Plugins.Implementations.Transform;

/// <summary>
/// Reranking plugin using cross-encoder or other methods.
/// Re-ranks documents for improved relevance ordering.
/// </summary>
[RagPlugin("transform-ranker-v1",
    Category = PluginCategory.Transform,
    Name = "Ranker",
    Description = "Re-ranks documents using cross-encoder or other methods",
    Author = "MeepleAI")]
public sealed class TransformRankerPlugin : RagPluginBase
{
    /// <inheritdoc />
    public override string Id => "transform-ranker-v1";

    /// <inheritdoc />
    public override string Name => "Ranker";

    /// <inheritdoc />
    public override string Version => "1.0.0";

    /// <inheritdoc />
    public override PluginCategory Category => PluginCategory.Transform;

    /// <inheritdoc />
    protected override string Description => "Re-ranks documents using cross-encoder or other methods";

    /// <inheritdoc />
    protected override IReadOnlyList<string> Tags => ["transform", "ranker", "rerank", "cross-encoder"];

    /// <inheritdoc />
    protected override IReadOnlyList<string> Capabilities => ["cross-encoder", "llm-rerank", "bm25"];

    public TransformRankerPlugin(ILogger<TransformRankerPlugin> logger) : base(logger)
    {
    }

    /// <inheritdoc />
    protected override async Task<PluginOutput> ExecuteCoreAsync(
        PluginInput input,
        PluginConfig config,
        CancellationToken cancellationToken)
    {
        var (query, documents) = ParsePayload(input.Payload);

        if (documents.Count == 0)
        {
            var emptyResult = JsonDocument.Parse("""{"ranked": [], "rerankTime": 0}""");
            return PluginOutput.Successful(input.ExecutionId, emptyResult);
        }

        var customConfig = ParseCustomConfig(config);
        var stopwatch = Stopwatch.StartNew();

        var ranked = customConfig.Method switch
        {
            "cross-encoder" => await RerankWithCrossEncoderAsync(query, documents, customConfig, cancellationToken).ConfigureAwait(false),
            "llm" => await RerankWithLlmAsync(query, documents, customConfig, cancellationToken).ConfigureAwait(false),
            "bm25" => RerankWithBm25(query, documents),
            _ => await RerankWithCrossEncoderAsync(query, documents, customConfig, cancellationToken).ConfigureAwait(false)
        };

        ranked = ranked.Take(customConfig.TopK).ToList();

        stopwatch.Stop();

        var result = JsonDocument.Parse(JsonSerializer.Serialize(new
        {
            ranked = ranked.Select(d => new
            {
                id = d.Id,
                content = d.Content,
                score = d.Score,
                source = d.Source
            }),
            rerankTime = stopwatch.Elapsed.TotalMilliseconds
        }));

        Logger.LogInformation(
            "Reranked documents: Method={Method}, Input={Input}, Output={Output}, Time={Time:F0}ms",
            customConfig.Method, documents.Count, ranked.Count, stopwatch.Elapsed.TotalMilliseconds);

        return new PluginOutput
        {
            ExecutionId = input.ExecutionId,
            Success = true,
            Result = result,
            Confidence = ranked.Count > 0 ? ranked[0].Score : 0,
            Metrics = new PluginExecutionMetrics
            {
                DurationMs = stopwatch.Elapsed.TotalMilliseconds,
                ItemsProcessed = ranked.Count
            }
        };
    }

    private static async Task<List<RankedDocument>> RerankWithCrossEncoderAsync(
        string query,
        List<DocumentItem> documents,
        RankerConfig config,
        CancellationToken cancellationToken)
    {
        // Simulate cross-encoder reranking (production calls reranker service)
        await Task.Delay(20, cancellationToken).ConfigureAwait(false);

        var random = new Random((query + documents.Count).GetHashCode(StringComparison.Ordinal));

        return documents
            .Select(d =>
            {
                // Simulate cross-encoder score (0-1 range)
                var baseScore = d.Score;
                var crossEncoderScore = Math.Clamp(baseScore + (random.NextDouble() - 0.5) * 0.2, 0, 1);
                return new RankedDocument(d.Id, d.Content, crossEncoderScore, d.Source);
            })
            .OrderByDescending(d => d.Score)
            .ToList();
    }

    private static async Task<List<RankedDocument>> RerankWithLlmAsync(
        string query,
        List<DocumentItem> documents,
        RankerConfig config,
        CancellationToken cancellationToken)
    {
        // Simulate LLM-based reranking
        await Task.Delay(50, cancellationToken).ConfigureAwait(false);

        var queryTerms = query.ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries);

        return documents
            .Select(d =>
            {
                var contentLower = d.Content.ToLowerInvariant();
                var matchCount = queryTerms.Count(t => contentLower.Contains(t, StringComparison.OrdinalIgnoreCase));
                var relevanceScore = queryTerms.Length > 0
                    ? (double)matchCount / queryTerms.Length
                    : 0.5;

                // Blend with original score
                var finalScore = (relevanceScore * 0.6) + (d.Score * 0.4);
                return new RankedDocument(d.Id, d.Content, finalScore, d.Source);
            })
            .OrderByDescending(d => d.Score)
            .ToList();
    }

    private static List<RankedDocument> RerankWithBm25(string query, List<DocumentItem> documents)
    {
        var queryTerms = query.ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var k1 = 1.2;
        var b = 0.75;
        var avgLen = documents.Average(d => d.Content.Split(' ').Length);

        return documents
            .Select(d =>
            {
                var docTerms = d.Content.ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries);
                var docLen = docTerms.Length;

                double bm25Score = 0;
                foreach (var term in queryTerms)
                {
                    var tf = docTerms.Count(t => string.Equals(t, term, StringComparison.Ordinal));
                    if (tf > 0)
                    {
                        var idf = Math.Log((documents.Count + 1.0) / (1 + documents.Count(doc =>
                            doc.Content.ToLowerInvariant().Contains(term, StringComparison.Ordinal))));
                        var numerator = tf * (k1 + 1);
                        var denominator = tf + k1 * (1 - b + b * docLen / avgLen);
                        bm25Score += idf * numerator / denominator;
                    }
                }

                // Normalize to 0-1 range
                var normalizedScore = Math.Clamp(bm25Score / 10, 0, 1);
                return new RankedDocument(d.Id, d.Content, normalizedScore, d.Source);
            })
            .OrderByDescending(d => d.Score)
            .ToList();
    }

    private static (string Query, List<DocumentItem> Documents) ParsePayload(JsonDocument payload)
    {
        var query = string.Empty;
        var documents = new List<DocumentItem>();

        if (payload.RootElement.TryGetProperty("query", out var queryElement))
        {
            query = queryElement.GetString() ?? string.Empty;
        }

        if (payload.RootElement.TryGetProperty("documents", out var docsElement) &&
            docsElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var doc in docsElement.EnumerateArray())
            {
                var id = doc.TryGetProperty("id", out var i) ? i.GetString() ?? Guid.NewGuid().ToString() : Guid.NewGuid().ToString();
                var content = doc.TryGetProperty("content", out var c) ? c.GetString() ?? "" : "";
                var score = doc.TryGetProperty("score", out var s) ? s.GetDouble() : 0.5;
                var source = doc.TryGetProperty("source", out var src) ? src.GetString() ?? "unknown" : "unknown";
                documents.Add(new DocumentItem(id, content, score, source));
            }
        }

        return (query, documents);
    }

    private static RankerConfig ParseCustomConfig(PluginConfig config)
    {
        if (config.CustomConfig == null)
        {
            return new RankerConfig();
        }

        var root = config.CustomConfig.RootElement;
        return new RankerConfig
        {
            Method = root.TryGetProperty("method", out var m) ? m.GetString() ?? "cross-encoder" : "cross-encoder",
            Model = root.TryGetProperty("model", out var mod) ? mod.GetString() : null,
            TopK = root.TryGetProperty("topK", out var tk) ? tk.GetInt32() : 10
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
                Message = "Query is required for reranking",
                PropertyPath = "payload.query",
                Code = "MISSING_QUERY"
            });
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
            "properties": {
                "query": { "type": "string" },
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
            "properties": {
                "ranked": { "type": "array" },
                "rerankTime": { "type": "number" }
            },
            "required": ["ranked", "rerankTime"]
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
                    "enum": ["cross-encoder", "llm", "bm25"],
                    "default": "cross-encoder"
                },
                "model": { "type": "string" },
                "topK": { "type": "integer", "minimum": 1, "default": 10 }
            }
        }
        """);
    }

    private sealed record DocumentItem(string Id, string Content, double Score, string Source);
    private sealed record RankedDocument(string Id, string Content, double Score, string Source);

    private sealed class RankerConfig
    {
        public string Method { get; init; } = "cross-encoder";
        public string? Model { get; init; }
        public int TopK { get; init; } = 10;
    }
}
