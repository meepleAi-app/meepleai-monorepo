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
/// Multi-source retrieval plugin with parallel search and fusion.
/// Retrieves from multiple knowledge bases and fuses results.
/// </summary>
[RagPlugin("retrieval-multi-source-v1",
    Category = PluginCategory.Retrieval,
    Name = "Multi-Source Retrieval",
    Description = "Parallel retrieval from multiple sources with configurable fusion",
    Author = "MeepleAI")]
public sealed class RetrievalMultiSourcePlugin : RagPluginBase
{
    /// <inheritdoc />
    public override string Id => "retrieval-multi-source-v1";

    /// <inheritdoc />
    public override string Name => "Multi-Source Retrieval";

    /// <inheritdoc />
    public override string Version => "1.0.0";

    /// <inheritdoc />
    public override PluginCategory Category => PluginCategory.Retrieval;

    /// <inheritdoc />
    protected override string Description => "Parallel retrieval from multiple sources with configurable fusion";

    /// <inheritdoc />
    protected override IReadOnlyList<string> Tags => ["retrieval", "multi-source", "parallel", "fusion"];

    /// <inheritdoc />
    protected override IReadOnlyList<string> Capabilities => ["multi-source", "parallel-search", "source-fusion"];

    private const int RrfK = 60;

    public RetrievalMultiSourcePlugin(ILogger<RetrievalMultiSourcePlugin> logger) : base(logger)
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

        // Search all sources in parallel
        var searchTasks = customConfig.Sources.Select(source =>
            SearchSourceAsync(query, source, cancellationToken));

        var allResults = await Task.WhenAll(searchTasks).ConfigureAwait(false);

        // Flatten and fuse results
        var sourceResults = allResults
            .SelectMany((results, i) => results.Select(r => (Source: customConfig.Sources[i], Doc: r)))
            .ToList();

        var fusedResults = FuseResults(sourceResults, customConfig);

        stopwatch.Stop();

        // Calculate source breakdown
        var sourceBreakdown = fusedResults
            .GroupBy(d => d.Source, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Count(), StringComparer.Ordinal);

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
            sourceBreakdown = sourceBreakdown
        }));

        Logger.LogInformation(
            "Multi-source retrieval: Sources={SourceCount}, Results={ResultCount}, Duration={Duration:F0}ms",
            customConfig.Sources.Count, fusedResults.Count, stopwatch.Elapsed.TotalMilliseconds);

        return new PluginOutput
        {
            ExecutionId = input.ExecutionId,
            Success = true,
            Result = result,
            Confidence = fusedResults.Count > 0 ? fusedResults.Max(d => d.Score) : 0,
            Metrics = new PluginExecutionMetrics
            {
                DurationMs = stopwatch.Elapsed.TotalMilliseconds,
                ItemsProcessed = fusedResults.Count
            }
        };
    }

    private static async Task<List<RetrievedDocument>> SearchSourceAsync(
        string query,
        SourceConfig source,
        CancellationToken cancellationToken)
    {
        // Simulate source search (production integrates with actual sources)
        await Task.Delay(15 + (source.Name.GetHashCode(StringComparison.Ordinal) % 10), cancellationToken).ConfigureAwait(false);

        var results = new List<RetrievedDocument>();
        var random = new Random(query.GetHashCode(StringComparison.Ordinal) + source.Name.GetHashCode(StringComparison.Ordinal));

        var count = random.Next(2, 8);
        for (int i = 0; i < count; i++)
        {
            var score = 0.90 - (i * 0.08) + (random.NextDouble() * 0.05);
            results.Add(RetrievedDocument.Create(
                $"{source.Name}-{Guid.NewGuid():N}",
                $"[{source.Name}] Result {i + 1}: Content related to query",
                score,
                source.Name,
                new Dictionary<string, object>(StringComparer.Ordinal)
                {
                    ["sourceType"] = source.Type,
                    ["rank"] = i + 1
                }));
        }

        return results;
    }

    private List<RetrievedDocument> FuseResults(
        List<(SourceConfig Source, RetrievedDocument Doc)> sourceResults,
        MultiSourceConfig config)
    {
        return config.FusionMethod switch
        {
            "rrf" => FuseWithRrf(sourceResults, config.TopK),
            "weighted" => FuseWithWeights(sourceResults, config),
            "max" => FuseWithMax(sourceResults, config.TopK),
            _ => FuseWithRrf(sourceResults, config.TopK)
        };
    }

    private static List<RetrievedDocument> FuseWithRrf(
        List<(SourceConfig Source, RetrievedDocument Doc)> sourceResults,
        int topK)
    {
        // Group by source and rank within source
        var sourceGroups = sourceResults
            .GroupBy(x => x.Source.Name, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Select(x => x.Doc).ToList(), StringComparer.Ordinal);

        var rrfScores = new Dictionary<string, (double Score, RetrievedDocument Doc)>(StringComparer.Ordinal);

        foreach (var (_, docs) in sourceGroups)
        {
            for (int i = 0; i < docs.Count; i++)
            {
                var doc = docs[i];
                var rrfScore = 1.0 / (RrfK + i + 1);

                if (rrfScores.TryGetValue(doc.Id, out var existing))
                {
                    rrfScores[doc.Id] = (existing.Score + rrfScore, doc);
                }
                else
                {
                    rrfScores[doc.Id] = (rrfScore, doc);
                }
            }
        }

        return rrfScores
            .OrderByDescending(kvp => kvp.Value.Score)
            .Take(topK)
            .Select(kvp => kvp.Value.Doc with { Score = kvp.Value.Score })
            .ToList();
    }

    private static List<RetrievedDocument> FuseWithWeights(
        List<(SourceConfig Source, RetrievedDocument Doc)> sourceResults,
        MultiSourceConfig config)
    {
        var weightedDocs = sourceResults
            .Select(x =>
            {
                var weight = x.Source.Weight;
                return x.Doc with { Score = x.Doc.Score * weight };
            })
            .OrderByDescending(d => d.Score)
            .Take(config.TopK)
            .ToList();

        return weightedDocs;
    }

    private static List<RetrievedDocument> FuseWithMax(
        List<(SourceConfig Source, RetrievedDocument Doc)> sourceResults,
        int topK)
    {
        // For each document ID, keep only the highest score
        var maxScores = new Dictionary<string, RetrievedDocument>(StringComparer.Ordinal);

        foreach (var (_, doc) in sourceResults)
        {
            if (!maxScores.TryGetValue(doc.Id, out var existing) || doc.Score > existing.Score)
            {
                maxScores[doc.Id] = doc;
            }
        }

        return maxScores.Values
            .OrderByDescending(d => d.Score)
            .Take(topK)
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

    private static MultiSourceConfig ParseCustomConfig(PluginConfig config)
    {
        var defaultSources = new List<SourceConfig>
        {
            new("rules-kb", "vector", 1.0),
            new("faq-kb", "vector", 0.8),
            new("game-docs", "hybrid", 0.9)
        };

        if (config.CustomConfig == null)
        {
            return new MultiSourceConfig { Sources = defaultSources };
        }

        var root = config.CustomConfig.RootElement;
        var sources = new List<SourceConfig>();

        if (root.TryGetProperty("sources", out var srcElement) && srcElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var src in srcElement.EnumerateArray())
            {
                var name = src.TryGetProperty("name", out var n) ? n.GetString() ?? "unknown" : "unknown";
                var type = src.TryGetProperty("type", out var t) ? t.GetString() ?? "vector" : "vector";
                var weight = src.TryGetProperty("weight", out var w) ? w.GetDouble() : 1.0;
                sources.Add(new SourceConfig(name, type, weight));
            }
        }

        return new MultiSourceConfig
        {
            Sources = sources.Count > 0 ? sources : defaultSources,
            FusionMethod = root.TryGetProperty("fusionMethod", out var fm) ? fm.GetString() ?? "rrf" : "rrf",
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
                Message = "Query is required in payload",
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
            "description": "Input for multi-source retrieval plugin",
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
            "description": "Output from multi-source retrieval plugin",
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
                "sourceBreakdown": {
                    "type": "object",
                    "additionalProperties": { "type": "integer" }
                }
            },
            "required": ["documents", "sourceBreakdown"]
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
            "description": "Configuration for multi-source retrieval plugin",
            "properties": {
                "sources": {
                    "type": "array",
                    "items": {
                        "type": "object",
                        "properties": {
                            "name": { "type": "string" },
                            "type": { "type": "string", "enum": ["vector", "hybrid", "keyword"] },
                            "weight": { "type": "number", "minimum": 0, "maximum": 1 }
                        },
                        "required": ["name"]
                    }
                },
                "fusionMethod": {
                    "type": "string",
                    "enum": ["rrf", "weighted", "max"],
                    "default": "rrf"
                },
                "topK": {
                    "type": "integer",
                    "minimum": 1,
                    "default": 10
                }
            }
        }
        """);
    }

    private sealed record SourceConfig(string Name, string Type, double Weight);

    private sealed class MultiSourceConfig
    {
        public IReadOnlyList<SourceConfig> Sources { get; init; } = [];
        public string FusionMethod { get; init; } = "rrf";
        public int TopK { get; init; } = 10;
    }
}
