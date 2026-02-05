// =============================================================================
// MeepleAI - RAG Plugin System
// Issue #3421 - Evaluation Plugins
// =============================================================================

using System.Text.Json;
using Api.BoundedContexts.KnowledgeBase.Domain.Plugins.Base;
using Api.BoundedContexts.KnowledgeBase.Domain.Plugins.Contracts;
using Api.BoundedContexts.KnowledgeBase.Domain.Plugins.Enums;
using Api.BoundedContexts.KnowledgeBase.Domain.Plugins.Models;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.KnowledgeBase.Domain.Plugins.Implementations.Evaluation;

/// <summary>
/// Multi-dimensional relevance scoring plugin.
/// Evaluates documents across semantic, keyword, recency, and authority dimensions.
/// </summary>
[RagPlugin("evaluation-relevance-scorer-v1",
    Category = PluginCategory.Evaluation,
    Name = "Relevance Scorer",
    Description = "Multi-dimensional relevance scoring across semantic, keyword, recency, authority",
    Author = "MeepleAI")]
public sealed class EvaluationRelevanceScorerPlugin : RagPluginBase
{
    /// <inheritdoc />
    public override string Id => "evaluation-relevance-scorer-v1";

    /// <inheritdoc />
    public override string Name => "Relevance Scorer";

    /// <inheritdoc />
    public override string Version => "1.0.0";

    /// <inheritdoc />
    public override PluginCategory Category => PluginCategory.Evaluation;

    /// <inheritdoc />
    protected override string Description => "Multi-dimensional relevance scoring";

    /// <inheritdoc />
    protected override IReadOnlyList<string> Tags => ["evaluation", "relevance", "multi-dimensional", "scoring"];

    /// <inheritdoc />
    protected override IReadOnlyList<string> Capabilities => ["semantic-scoring", "keyword-scoring", "recency-scoring", "authority-scoring"];

    private static readonly Dictionary<string, double> DefaultWeights = new(StringComparer.Ordinal)
    {
        ["semantic"] = 0.4,
        ["keyword"] = 0.3,
        ["recency"] = 0.15,
        ["authority"] = 0.15
    };

    public EvaluationRelevanceScorerPlugin(ILogger<EvaluationRelevanceScorerPlugin> logger) : base(logger)
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
            var emptyResult = JsonDocument.Parse(JsonSerializer.Serialize(new
            {
                overallScore = 0.0,
                dimensionScores = new Dictionary<string, double>(StringComparer.Ordinal),
                topDocuments = Array.Empty<string>()
            }));
            return PluginOutput.Successful(input.ExecutionId, emptyResult, 0);
        }

        var customConfig = ParseCustomConfig(config);

        // Score each document across dimensions
        var scoredDocs = await ScoreDocumentsAsync(query, documents, customConfig, cancellationToken).ConfigureAwait(false);

        // Calculate overall score
        var overallScore = scoredDocs.Count > 0
            ? scoredDocs.Take(3).Average(d => d.OverallScore)
            : 0;

        // Aggregate dimension scores
        var dimensionScores = customConfig.Dimensions.ToDictionary(
            d => d,
            d => scoredDocs.Count > 0
                ? scoredDocs.Take(5).Average(doc => doc.DimensionScores.GetValueOrDefault(d, 0))
                : 0,
            StringComparer.Ordinal);

        // Get top document IDs
        var topDocuments = scoredDocs
            .OrderByDescending(d => d.OverallScore)
            .Take(5)
            .Select(d => d.Id)
            .ToList();

        var result = JsonDocument.Parse(JsonSerializer.Serialize(new
        {
            overallScore = overallScore,
            dimensionScores = dimensionScores,
            topDocuments = topDocuments
        }));

        Logger.LogInformation(
            "Relevance scoring: OverallScore={Score:F2}, TopDocs={TopCount}, Dimensions={DimCount}",
            overallScore, topDocuments.Count, dimensionScores.Count);

        return PluginOutput.Successful(input.ExecutionId, result, overallScore);
    }

    private static async Task<List<ScoredDocument>> ScoreDocumentsAsync(
        string query,
        List<DocumentInfo> documents,
        RelevanceConfig config,
        CancellationToken cancellationToken)
    {
        // Simulate multi-dimensional scoring
        await Task.Delay(15, cancellationToken).ConfigureAwait(false);

        var random = new Random(query.GetHashCode(StringComparison.Ordinal));
        var scoredDocs = new List<ScoredDocument>();

        foreach (var doc in documents)
        {
            var dimensionScores = new Dictionary<string, double>(StringComparer.Ordinal);

            // Calculate each dimension score
            foreach (var dimension in config.Dimensions)
            {
                var baseScore = dimension switch
                {
                    "semantic" => doc.Score, // Use existing score as semantic base
                    "keyword" => CalculateKeywordScore(query, doc.Content),
                    "recency" => 0.7 + random.NextDouble() * 0.3, // Simulated
                    "authority" => 0.6 + random.NextDouble() * 0.4, // Simulated
                    _ => 0.5
                };

                dimensionScores[dimension] = Math.Clamp(baseScore, 0, 1);
            }

            // Calculate weighted overall score
            var overallScore = config.Dimensions
                .Sum(d => dimensionScores.GetValueOrDefault(d, 0) * config.Weights.GetValueOrDefault(d, 0.25));

            scoredDocs.Add(new ScoredDocument(doc.Id, overallScore, dimensionScores));
        }

        return scoredDocs.OrderByDescending(d => d.OverallScore).ToList();
    }

    private static double CalculateKeywordScore(string query, string content)
    {
        if (string.IsNullOrEmpty(query) || string.IsNullOrEmpty(content))
        {
            return 0;
        }

        var queryTerms = query.ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var contentLower = content.ToLowerInvariant();

        var matchedTerms = queryTerms.Count(term => contentLower.Contains(term, StringComparison.Ordinal));
        return queryTerms.Length > 0 ? (double)matchedTerms / queryTerms.Length : 0;
    }

    private static (string Query, List<DocumentInfo> Documents) ParsePayload(JsonDocument payload)
    {
        var query = string.Empty;
        var documents = new List<DocumentInfo>();

        if (payload.RootElement.TryGetProperty("query", out var queryElement))
        {
            query = queryElement.GetString() ?? string.Empty;
        }

        if (payload.RootElement.TryGetProperty("documents", out var docsElement) &&
            docsElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var doc in docsElement.EnumerateArray())
            {
                var id = doc.TryGetProperty("id", out var i) ? i.GetString() ?? "" : Guid.NewGuid().ToString();
                var content = doc.TryGetProperty("content", out var c) ? c.GetString() ?? "" : "";
                var score = doc.TryGetProperty("score", out var s) ? s.GetDouble() : 0.5;
                documents.Add(new DocumentInfo(id, content, score));
            }
        }

        return (query, documents);
    }

    private static RelevanceConfig ParseCustomConfig(PluginConfig config)
    {
        if (config.CustomConfig == null)
        {
            return new RelevanceConfig();
        }

        var root = config.CustomConfig.RootElement;
        var dimensions = new List<string>();
        var weights = new Dictionary<string, double>(StringComparer.Ordinal);

        if (root.TryGetProperty("dimensions", out var dimsElement) && dimsElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var dim in dimsElement.EnumerateArray())
            {
                var d = dim.GetString();
                if (!string.IsNullOrEmpty(d))
                {
                    dimensions.Add(d);
                }
            }
        }

        if (root.TryGetProperty("weights", out var weightsElement) && weightsElement.ValueKind == JsonValueKind.Object)
        {
            foreach (var prop in weightsElement.EnumerateObject())
            {
                weights[prop.Name] = prop.Value.GetDouble();
            }
        }

        return new RelevanceConfig
        {
            Dimensions = dimensions.Count > 0 ? dimensions : ["semantic", "keyword", "recency", "authority"],
            Weights = weights.Count > 0 ? weights : DefaultWeights
        };
    }

    /// <inheritdoc />
    protected override JsonDocument CreateInputSchema()
    {
        return CreateSchemaFromJson("""
        {
            "$schema": "https://json-schema.org/draft/2020-12/schema",
            "type": "object",
            "description": "Input for relevance scorer plugin",
            "properties": {
                "query": {
                    "type": "string",
                    "description": "The original query"
                },
                "documents": {
                    "type": "array",
                    "description": "Documents to score",
                    "items": {
                        "type": "object",
                        "properties": {
                            "id": { "type": "string" },
                            "content": { "type": "string" },
                            "score": { "type": "number" }
                        }
                    }
                }
            }
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
            "description": "Output from relevance scorer plugin",
            "properties": {
                "overallScore": {
                    "type": "number",
                    "minimum": 0,
                    "maximum": 1,
                    "description": "Weighted overall relevance score"
                },
                "dimensionScores": {
                    "type": "object",
                    "additionalProperties": { "type": "number" },
                    "description": "Scores per dimension"
                },
                "topDocuments": {
                    "type": "array",
                    "items": { "type": "string" },
                    "description": "IDs of top scoring documents"
                }
            },
            "required": ["overallScore", "dimensionScores", "topDocuments"]
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
            "description": "Configuration for relevance scorer plugin",
            "properties": {
                "dimensions": {
                    "type": "array",
                    "items": { "type": "string" },
                    "default": ["semantic", "keyword", "recency", "authority"]
                },
                "weights": {
                    "type": "object",
                    "additionalProperties": { "type": "number" }
                }
            }
        }
        """);
    }

    private sealed record DocumentInfo(string Id, string Content, double Score);
    private sealed record ScoredDocument(string Id, double OverallScore, Dictionary<string, double> DimensionScores);

    private sealed class RelevanceConfig
    {
        public IReadOnlyList<string> Dimensions { get; init; } = ["semantic", "keyword", "recency", "authority"];
        public IReadOnlyDictionary<string, double> Weights { get; init; } = DefaultWeights;
    }
}
