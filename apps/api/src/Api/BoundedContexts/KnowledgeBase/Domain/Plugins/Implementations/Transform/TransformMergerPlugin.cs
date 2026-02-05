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

namespace Api.BoundedContexts.KnowledgeBase.Domain.Plugins.Implementations.Transform;

/// <summary>
/// Merger plugin for combining multiple inputs into a single output.
/// Supports concat, RRF, weighted, and dedupe merge strategies.
/// </summary>
[RagPlugin("transform-merger-v1",
    Category = PluginCategory.Transform,
    Name = "Merger",
    Description = "Merges multiple inputs into a single output with configurable strategies",
    Author = "MeepleAI")]
public sealed class TransformMergerPlugin : RagPluginBase
{
    /// <inheritdoc />
    public override string Id => "transform-merger-v1";

    /// <inheritdoc />
    public override string Name => "Merger";

    /// <inheritdoc />
    public override string Version => "1.0.0";

    /// <inheritdoc />
    public override PluginCategory Category => PluginCategory.Transform;

    /// <inheritdoc />
    protected override string Description => "Merges multiple inputs with configurable strategies";

    /// <inheritdoc />
    protected override IReadOnlyList<string> Tags => ["transform", "merger", "fusion", "combine"];

    /// <inheritdoc />
    protected override IReadOnlyList<string> Capabilities => ["concat", "rrf", "weighted", "dedupe"];

    private const int RrfK = 60;

    public TransformMergerPlugin(ILogger<TransformMergerPlugin> logger) : base(logger)
    {
    }

    /// <inheritdoc />
    protected override Task<PluginOutput> ExecuteCoreAsync(
        PluginInput input,
        PluginConfig config,
        CancellationToken cancellationToken)
    {
        var items = ParseItems(input.Payload);
        var customConfig = ParseCustomConfig(config);

        var merged = customConfig.MergeStrategy switch
        {
            "rrf" => MergeWithRrf(items, customConfig),
            "weighted" => MergeWithWeights(items, customConfig),
            "dedupe" => MergeWithDedupe(items, customConfig),
            _ => MergeWithConcat(items, customConfig)
        };

        var sourceCounts = items
            .GroupBy(i => i.Source, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Count(), StringComparer.Ordinal);

        var result = JsonDocument.Parse(JsonSerializer.Serialize(new
        {
            merged = merged,
            sourceCounts = sourceCounts
        }));

        Logger.LogInformation(
            "Merged items: Strategy={Strategy}, InputCount={Input}, OutputCount={Output}",
            customConfig.MergeStrategy, items.Count, merged.Count);

        return Task.FromResult(PluginOutput.Successful(input.ExecutionId, result));
    }

    private static List<MergedItem> MergeWithConcat(List<SourceItem> items, MergerConfig config)
    {
        return items
            .Select(i => new MergedItem(i.Id, i.Content, i.Score, i.Source))
            .Take(config.MaxItems ?? int.MaxValue)
            .ToList();
    }

    private static List<MergedItem> MergeWithRrf(List<SourceItem> items, MergerConfig config)
    {
        var grouped = items.GroupBy(i => i.Source, StringComparer.Ordinal);
        var rrfScores = new Dictionary<string, (double Score, SourceItem Item)>(StringComparer.Ordinal);

        foreach (var group in grouped)
        {
            var orderedItems = group.OrderByDescending(i => i.Score).ToList();
            for (int i = 0; i < orderedItems.Count; i++)
            {
                var item = orderedItems[i];
                var rrfScore = 1.0 / (RrfK + i + 1);

                if (rrfScores.TryGetValue(item.Id, out var existing))
                {
                    rrfScores[item.Id] = (existing.Score + rrfScore, item);
                }
                else
                {
                    rrfScores[item.Id] = (rrfScore, item);
                }
            }
        }

        return rrfScores
            .OrderByDescending(kvp => kvp.Value.Score)
            .Take(config.MaxItems ?? int.MaxValue)
            .Select(kvp => new MergedItem(kvp.Key, kvp.Value.Item.Content, kvp.Value.Score, kvp.Value.Item.Source))
            .ToList();
    }

    private static List<MergedItem> MergeWithWeights(List<SourceItem> items, MergerConfig config)
    {
        return items
            .Select(i =>
            {
                var weight = config.Weights.GetValueOrDefault(i.Source, 1.0);
                return new MergedItem(i.Id, i.Content, i.Score * weight, i.Source);
            })
            .OrderByDescending(i => i.Score)
            .Take(config.MaxItems ?? int.MaxValue)
            .ToList();
    }

    private static List<MergedItem> MergeWithDedupe(List<SourceItem> items, MergerConfig config)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var deduped = new List<MergedItem>();

        foreach (var item in items.OrderByDescending(i => i.Score))
        {
            if (seen.Add(item.Id))
            {
                deduped.Add(new MergedItem(item.Id, item.Content, item.Score, item.Source));
            }

            if (config.MaxItems.HasValue && deduped.Count >= config.MaxItems.Value)
            {
                break;
            }
        }

        return deduped;
    }

    private static List<SourceItem> ParseItems(JsonDocument payload)
    {
        var items = new List<SourceItem>();

        if (payload.RootElement.TryGetProperty("items", out var itemsElement) &&
            itemsElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in itemsElement.EnumerateArray())
            {
                var id = item.TryGetProperty("id", out var i) ? i.GetString() ?? Guid.NewGuid().ToString() : Guid.NewGuid().ToString();
                var content = item.TryGetProperty("content", out var c) ? c.GetString() ?? "" : "";
                var score = item.TryGetProperty("score", out var s) ? s.GetDouble() : 1.0;
                var source = item.TryGetProperty("source", out var src) ? src.GetString() ?? "unknown" : "unknown";
                items.Add(new SourceItem(id, content, score, source));
            }
        }

        return items;
    }

    private static MergerConfig ParseCustomConfig(PluginConfig config)
    {
        if (config.CustomConfig == null)
        {
            return new MergerConfig();
        }

        var root = config.CustomConfig.RootElement;
        var weights = new Dictionary<string, double>(StringComparer.Ordinal);

        if (root.TryGetProperty("weights", out var weightsElement) &&
            weightsElement.ValueKind == JsonValueKind.Object)
        {
            foreach (var prop in weightsElement.EnumerateObject())
            {
                weights[prop.Name] = prop.Value.GetDouble();
            }
        }

        return new MergerConfig
        {
            MergeStrategy = root.TryGetProperty("mergeStrategy", out var ms) ? ms.GetString() ?? "concat" : "concat",
            Weights = weights,
            MaxItems = root.TryGetProperty("maxItems", out var mi) ? mi.GetInt32() : null
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
                "items": {
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
            "required": ["items"]
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
                "merged": { "type": "array" },
                "sourceCounts": { "type": "object" }
            },
            "required": ["merged", "sourceCounts"]
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
                "mergeStrategy": {
                    "type": "string",
                    "enum": ["concat", "rrf", "weighted", "dedupe"],
                    "default": "concat"
                },
                "weights": {
                    "type": "object",
                    "additionalProperties": { "type": "number" }
                },
                "maxItems": {
                    "type": "integer",
                    "minimum": 1
                }
            }
        }
        """);
    }

    private sealed record SourceItem(string Id, string Content, double Score, string Source);
    private sealed record MergedItem(string Id, string Content, double Score, string Source);

    private sealed class MergerConfig
    {
        public string MergeStrategy { get; init; } = "concat";
        public IReadOnlyDictionary<string, double> Weights { get; init; } = new Dictionary<string, double>(StringComparer.Ordinal);
        public int? MaxItems { get; init; }
    }
}
