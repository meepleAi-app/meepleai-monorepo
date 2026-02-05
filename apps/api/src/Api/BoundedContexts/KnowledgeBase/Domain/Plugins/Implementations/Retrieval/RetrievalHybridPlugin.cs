// =============================================================================
// MeepleAI - RAG Plugin System
// Issue #3420 - Retrieval Plugins
// =============================================================================

using System.Diagnostics;
using System.Text.Json;
using Api.BoundedContexts.KnowledgeBase.Domain.Plugins.Base;
using Api.BoundedContexts.KnowledgeBase.Domain.Plugins.Contracts;
using Api.BoundedContexts.KnowledgeBase.Domain.Plugins.Enums;
using Api.BoundedContexts.KnowledgeBase.Domain.Plugins.Models;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.KnowledgeBase.Domain.Plugins.Implementations.Retrieval;

/// <summary>
/// Hybrid retrieval plugin combining vector similarity with keyword search using RRF.
/// Provides the best of both semantic and lexical matching.
/// </summary>
[RagPlugin("retrieval-hybrid-v1",
    Category = PluginCategory.Retrieval,
    Name = "Hybrid Retrieval",
    Description = "Combines vector similarity with keyword search using Reciprocal Rank Fusion",
    Author = "MeepleAI")]
public sealed class RetrievalHybridPlugin : RagPluginBase
{
    /// <inheritdoc />
    public override string Id => "retrieval-hybrid-v1";

    /// <inheritdoc />
    public override string Name => "Hybrid Retrieval";

    /// <inheritdoc />
    public override string Version => "1.0.0";

    /// <inheritdoc />
    public override PluginCategory Category => PluginCategory.Retrieval;

    /// <inheritdoc />
    protected override string Description => "Combines vector similarity with keyword search using RRF";

    /// <inheritdoc />
    protected override IReadOnlyList<string> Tags => ["retrieval", "hybrid", "vector", "keyword", "rrf"];

    /// <inheritdoc />
    protected override IReadOnlyList<string> Capabilities => ["vector-search", "keyword-search", "rrf-fusion"];

    private const int RrfK = 60; // RRF constant

    public RetrievalHybridPlugin(ILogger<RetrievalHybridPlugin> logger) : base(logger)
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
        var stopwatch = Stopwatch.StartNew();

        // Perform vector and keyword searches in parallel
        var vectorTask = PerformVectorSearchAsync(query, customConfig, cancellationToken);
        var keywordTask = PerformKeywordSearchAsync(query, customConfig, cancellationToken);

        await Task.WhenAll(vectorTask, keywordTask).ConfigureAwait(false);

        var vectorResults = await vectorTask.ConfigureAwait(false);
        var keywordResults = await keywordTask.ConfigureAwait(false);

        // Apply RRF fusion
        var fusionStopwatch = Stopwatch.StartNew();
        var fusedResults = ApplyRrfFusion(vectorResults, keywordResults, customConfig);
        fusionStopwatch.Stop();

        stopwatch.Stop();

        var metrics = new RetrievalMetrics
        {
            VectorHits = vectorResults.Count,
            KeywordHits = keywordResults.Count,
            FusionTimeMs = fusionStopwatch.Elapsed.TotalMilliseconds,
            TotalTimeMs = stopwatch.Elapsed.TotalMilliseconds
        };

        var result = JsonDocument.Parse(JsonSerializer.Serialize(new
        {
            documents = fusedResults.Select(d => new
            {
                id = d.Id,
                content = d.Content,
                score = d.Score,
                source = d.Source,
                metadata = d.Metadata
            }),
            searchMetrics = new
            {
                vectorHits = metrics.VectorHits,
                keywordHits = metrics.KeywordHits,
                fusionTime = metrics.FusionTimeMs
            }
        }));

        Logger.LogInformation(
            "Hybrid retrieval: VectorHits={VectorHits}, KeywordHits={KeywordHits}, FusedResults={FusedCount}, Duration={Duration:F0}ms",
            metrics.VectorHits, metrics.KeywordHits, fusedResults.Count, metrics.TotalTimeMs);

        return new PluginOutput
        {
            ExecutionId = input.ExecutionId,
            Success = true,
            Result = result,
            Confidence = fusedResults.Count > 0 ? fusedResults.Max(d => d.Score) : 0,
            Metrics = new PluginExecutionMetrics
            {
                DurationMs = metrics.TotalTimeMs,
                ItemsProcessed = fusedResults.Count
            }
        };
    }

    private static async Task<List<RetrievedDocument>> PerformVectorSearchAsync(
        string query,
        HybridConfig config,
        CancellationToken cancellationToken)
    {
        // Simulate vector search (production integrates with Qdrant)
        await Task.Delay(20, cancellationToken).ConfigureAwait(false);

        var results = new List<RetrievedDocument>();
        var random = new Random(query.GetHashCode(StringComparison.Ordinal));

        // Generate simulated vector search results
        var count = random.Next(3, 8);
        for (int i = 0; i < count; i++)
        {
            var score = 0.95 - (i * 0.08) + (random.NextDouble() * 0.05);
            if (score >= config.MinScore)
            {
                results.Add(RetrievedDocument.Create(
                    $"vec-{Guid.NewGuid():N}",
                    $"Vector result {i + 1}: Content related to '{query}'",
                    score,
                    config.Collections.Count > 0 ? config.Collections[0] : "default",
                    new Dictionary<string, object>(StringComparer.Ordinal)
                    {
                        ["searchType"] = "vector",
                        ["rank"] = i + 1
                    }));
            }
        }

        return results.Take(config.TopK).ToList();
    }

    private static async Task<List<RetrievedDocument>> PerformKeywordSearchAsync(
        string query,
        HybridConfig config,
        CancellationToken cancellationToken)
    {
        // Simulate keyword search (production integrates with PostgreSQL FTS)
        await Task.Delay(15, cancellationToken).ConfigureAwait(false);

        var results = new List<RetrievedDocument>();
        var random = new Random(query.GetHashCode(StringComparison.Ordinal) + 1);

        // Generate simulated keyword search results
        var count = random.Next(2, 6);
        for (int i = 0; i < count; i++)
        {
            var score = 0.90 - (i * 0.10) + (random.NextDouble() * 0.05);
            if (score >= config.MinScore)
            {
                results.Add(RetrievedDocument.Create(
                    $"kw-{Guid.NewGuid():N}",
                    $"Keyword result {i + 1}: Matching terms in '{query}'",
                    score,
                    config.Collections.Count > 0 ? config.Collections[0] : "default",
                    new Dictionary<string, object>(StringComparer.Ordinal)
                    {
                        ["searchType"] = "keyword",
                        ["rank"] = i + 1
                    }));
            }
        }

        return results.Take(config.TopK).ToList();
    }

    private List<RetrievedDocument> ApplyRrfFusion(
        List<RetrievedDocument> vectorResults,
        List<RetrievedDocument> keywordResults,
        HybridConfig config)
    {
        var rrfScores = new Dictionary<string, (double Score, RetrievedDocument Doc)>(StringComparer.Ordinal);

        // Calculate RRF scores for vector results
        for (int i = 0; i < vectorResults.Count; i++)
        {
            var doc = vectorResults[i];
            var rrfScore = config.VectorWeight / (RrfK + i + 1);
            rrfScores[doc.Id] = (rrfScore, doc);
        }

        // Add RRF scores for keyword results
        for (int i = 0; i < keywordResults.Count; i++)
        {
            var doc = keywordResults[i];
            var rrfScore = config.KeywordWeight / (RrfK + i + 1);

            if (rrfScores.TryGetValue(doc.Id, out var existing))
            {
                // Document appears in both - add scores
                rrfScores[doc.Id] = (existing.Score + rrfScore, doc);
            }
            else
            {
                rrfScores[doc.Id] = (rrfScore, doc);
            }
        }

        // Sort by RRF score and return top K
        return rrfScores
            .OrderByDescending(kvp => kvp.Value.Score)
            .Take(config.TopK)
            .Select(kvp => kvp.Value.Doc with
            {
                Score = kvp.Value.Score,
                Metadata = new Dictionary<string, object>(kvp.Value.Doc.Metadata, StringComparer.Ordinal)
                {
                    ["rrfScore"] = kvp.Value.Score
                }
            })
            .ToList();
    }

    private static string GetQueryFromPayload(JsonDocument payload)
    {
        if (payload.RootElement.TryGetProperty("query", out var queryElement))
        {
            return queryElement.GetString() ?? string.Empty;
        }
        return string.Empty;
    }

    private static HybridConfig ParseCustomConfig(PluginConfig config)
    {
        if (config.CustomConfig == null)
        {
            return new HybridConfig();
        }

        var root = config.CustomConfig.RootElement;
        var collections = new List<string>();

        if (root.TryGetProperty("collections", out var colElement) && colElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in colElement.EnumerateArray())
            {
                var col = item.GetString();
                if (!string.IsNullOrEmpty(col))
                {
                    collections.Add(col);
                }
            }
        }

        return new HybridConfig
        {
            VectorWeight = root.TryGetProperty("vectorWeight", out var vw) ? vw.GetDouble() : 0.7,
            KeywordWeight = root.TryGetProperty("keywordWeight", out var kw) ? kw.GetDouble() : 0.3,
            TopK = root.TryGetProperty("topK", out var tk) ? tk.GetInt32() : 10,
            MinScore = root.TryGetProperty("minScore", out var ms) ? ms.GetDouble() : 0.5,
            Collections = collections.Count > 0 ? collections : ["default"]
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

            if (root.TryGetProperty("vectorWeight", out var vw) && root.TryGetProperty("keywordWeight", out var kw))
            {
                var total = vw.GetDouble() + kw.GetDouble();
                if (Math.Abs(total - 1.0) > 0.01)
                {
                    errors.Add(new ValidationError
                    {
                        Message = "vectorWeight + keywordWeight should equal 1.0",
                        PropertyPath = "customConfig",
                        Code = "INVALID_WEIGHTS",
                        AttemptedValue = total
                    });
                }
            }

            if (root.TryGetProperty("topK", out var tk) && tk.GetInt32() <= 0)
            {
                errors.Add(new ValidationError
                {
                    Message = "topK must be greater than 0",
                    PropertyPath = "customConfig.topK",
                    Code = "INVALID_TOP_K"
                });
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
            "description": "Input for hybrid retrieval plugin",
            "properties": {
                "query": {
                    "type": "string",
                    "description": "The search query"
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
            "description": "Output from hybrid retrieval plugin",
            "properties": {
                "documents": {
                    "type": "array",
                    "items": {
                        "type": "object",
                        "properties": {
                            "id": { "type": "string" },
                            "content": { "type": "string" },
                            "score": { "type": "number" },
                            "source": { "type": "string" },
                            "metadata": { "type": "object" }
                        }
                    }
                },
                "searchMetrics": {
                    "type": "object",
                    "properties": {
                        "vectorHits": { "type": "integer" },
                        "keywordHits": { "type": "integer" },
                        "fusionTime": { "type": "number" }
                    }
                }
            },
            "required": ["documents", "searchMetrics"]
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
            "description": "Configuration for hybrid retrieval plugin",
            "properties": {
                "vectorWeight": {
                    "type": "number",
                    "minimum": 0,
                    "maximum": 1,
                    "default": 0.7
                },
                "keywordWeight": {
                    "type": "number",
                    "minimum": 0,
                    "maximum": 1,
                    "default": 0.3
                },
                "topK": {
                    "type": "integer",
                    "minimum": 1,
                    "default": 10
                },
                "minScore": {
                    "type": "number",
                    "minimum": 0,
                    "maximum": 1,
                    "default": 0.5
                },
                "collections": {
                    "type": "array",
                    "items": { "type": "string" },
                    "description": "Qdrant collections to search"
                }
            }
        }
        """);
    }

    private sealed class HybridConfig
    {
        public double VectorWeight { get; init; } = 0.7;
        public double KeywordWeight { get; init; } = 0.3;
        public int TopK { get; init; } = 10;
        public double MinScore { get; init; } = 0.5;
        public IReadOnlyList<string> Collections { get; init; } = ["default"];
    }
}
