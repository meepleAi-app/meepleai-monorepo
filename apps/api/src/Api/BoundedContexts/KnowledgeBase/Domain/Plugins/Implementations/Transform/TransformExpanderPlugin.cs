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
/// Query expansion plugin for enriching queries with synonyms and related terms.
/// Improves retrieval recall through query augmentation.
/// </summary>
[RagPlugin("transform-expander-v1",
    Category = PluginCategory.Transform,
    Name = "Query Expander",
    Description = "Expands query with synonyms and related terms",
    Author = "MeepleAI")]
public sealed class TransformExpanderPlugin : RagPluginBase
{
    /// <inheritdoc />
    public override string Id => "transform-expander-v1";

    /// <inheritdoc />
    public override string Name => "Query Expander";

    /// <inheritdoc />
    public override string Version => "1.0.0";

    /// <inheritdoc />
    public override PluginCategory Category => PluginCategory.Transform;

    /// <inheritdoc />
    protected override string Description => "Expands query with synonyms and related terms";

    /// <inheritdoc />
    protected override IReadOnlyList<string> Tags => ["transform", "expander", "query", "synonyms"];

    /// <inheritdoc />
    protected override IReadOnlyList<string> Capabilities => ["llm-expansion", "wordnet", "embedding-expansion"];

    // Board game domain synonyms
    private static readonly Dictionary<string, string[]> DomainSynonyms = new(StringComparer.OrdinalIgnoreCase)
    {
        ["rules"] = ["regulation", "guidelines", "mechanics", "instructions"],
        ["card"] = ["playing card", "game card", "deck card"],
        ["dice"] = ["die", "d6", "d20", "polyhedral"],
        ["player"] = ["participant", "gamer", "contestant"],
        ["turn"] = ["round", "move", "action phase"],
        ["win"] = ["victory", "triumph", "succeed", "beat"],
        ["setup"] = ["prepare", "initialize", "start game", "game preparation"],
        ["score"] = ["points", "victory points", "VP", "scoring"],
        ["strategy"] = ["tactics", "approach", "game plan", "method"],
        ["resource"] = ["asset", "material", "currency", "component"]
    };

    public TransformExpanderPlugin(ILogger<TransformExpanderPlugin> logger) : base(logger)
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
            return PluginOutput.Failed(input.ExecutionId, "Query is required", "MISSING_QUERY");
        }

        var customConfig = ParseCustomConfig(config);

        var expandedQueries = customConfig.Method switch
        {
            "llm" => await ExpandWithLlmAsync(query, customConfig, cancellationToken).ConfigureAwait(false),
            "embedding" => await ExpandWithEmbeddingAsync(query, customConfig, cancellationToken).ConfigureAwait(false),
            _ => ExpandWithWordnet(query, customConfig)
        };

        // Limit expansions
        expandedQueries = expandedQueries.Take(customConfig.MaxExpansions).ToList();

        var result = JsonDocument.Parse(JsonSerializer.Serialize(new
        {
            originalQuery = query,
            expandedQueries = expandedQueries,
            expansionMethod = customConfig.Method
        }));

        Logger.LogInformation(
            "Query expansion: Method={Method}, Original='{Query}', Expansions={Count}",
            customConfig.Method, query.Length > 30 ? query[..30] + "..." : query, expandedQueries.Count);

        return PluginOutput.Successful(input.ExecutionId, result);
    }

    private static List<string> ExpandWithWordnet(string query, ExpanderConfig config)
    {
        var expansions = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { query };
        var words = query.ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries);

        foreach (var word in words)
        {
            if (DomainSynonyms.TryGetValue(word, out var synonyms))
            {
                foreach (var synonym in synonyms.Take(2))
                {
                    var expanded = query.Replace(word, synonym, StringComparison.OrdinalIgnoreCase);
                    expansions.Add(expanded);
                }
            }
        }

        return expansions.Skip(1).ToList(); // Skip original
    }

    private static async Task<List<string>> ExpandWithLlmAsync(
        string query,
        ExpanderConfig config,
        CancellationToken cancellationToken)
    {
        // Simulate LLM-based expansion
        await Task.Delay(30, cancellationToken).ConfigureAwait(false);

        var expansions = new List<string>();

        // Generate variations
        expansions.Add($"What are the {query.ToLowerInvariant()}");
        expansions.Add($"How does {query.ToLowerInvariant()} work");
        expansions.Add($"Explain {query.ToLowerInvariant()}");

        // Also add wordnet expansions
        expansions.AddRange(ExpandWithWordnet(query, config));

        return expansions.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static async Task<List<string>> ExpandWithEmbeddingAsync(
        string query,
        ExpanderConfig config,
        CancellationToken cancellationToken)
    {
        // Simulate embedding-based expansion (find similar queries)
        await Task.Delay(20, cancellationToken).ConfigureAwait(false);

        var expansions = new List<string>();

        // Add slight variations based on query structure
        var words = query.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        if (words.Length > 2)
        {
            // Reorder words
            var reordered = string.Join(" ", words.Reverse());
            expansions.Add(reordered);

            // Drop optional words
            var shortened = string.Join(" ", words.Where((w, i) => i == 0 || i == words.Length - 1));
            if (shortened.Length > 3)
            {
                expansions.Add(shortened);
            }
        }

        // Add wordnet expansions
        expansions.AddRange(ExpandWithWordnet(query, config));

        return expansions.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static string GetQueryFromPayload(JsonDocument payload)
    {
        if (payload.RootElement.TryGetProperty("query", out var queryElement))
        {
            return queryElement.GetString() ?? string.Empty;
        }
        return string.Empty;
    }

    private static ExpanderConfig ParseCustomConfig(PluginConfig config)
    {
        if (config.CustomConfig == null)
        {
            return new ExpanderConfig();
        }

        var root = config.CustomConfig.RootElement;
        return new ExpanderConfig
        {
            Method = root.TryGetProperty("method", out var m) ? m.GetString() ?? "wordnet" : "wordnet",
            MaxExpansions = root.TryGetProperty("maxExpansions", out var me) ? me.GetInt32() : 5
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
                Message = "Query is required for expansion",
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
                "query": { "type": "string" }
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
                "originalQuery": { "type": "string" },
                "expandedQueries": { "type": "array", "items": { "type": "string" } },
                "expansionMethod": { "type": "string" }
            },
            "required": ["originalQuery", "expandedQueries", "expansionMethod"]
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
                    "enum": ["llm", "wordnet", "embedding"],
                    "default": "wordnet"
                },
                "maxExpansions": {
                    "type": "integer",
                    "minimum": 1,
                    "default": 5
                }
            }
        }
        """);
    }

    private sealed class ExpanderConfig
    {
        public string Method { get; init; } = "wordnet";
        public int MaxExpansions { get; init; } = 5;
    }
}
