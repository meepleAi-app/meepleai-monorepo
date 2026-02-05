// =============================================================================
// MeepleAI - RAG Plugin System
// Issue #3418 - Routing Plugins
// =============================================================================

using System.Text.Json;
using Api.BoundedContexts.KnowledgeBase.Domain.Plugins.Base;
using Api.BoundedContexts.KnowledgeBase.Domain.Plugins.Contracts;
using Api.BoundedContexts.KnowledgeBase.Domain.Plugins.Enums;
using Api.BoundedContexts.KnowledgeBase.Domain.Plugins.Models;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.KnowledgeBase.Domain.Plugins.Implementations.Routing;

/// <summary>
/// LLM-based routing plugin that classifies query intent and selects appropriate strategy.
/// Uses language models to understand query semantics and determine optimal processing path.
/// </summary>
[RagPlugin("routing-llm-v1",
    Category = PluginCategory.Routing,
    Name = "LLM Routing",
    Description = "Uses LLM to classify query intent and select processing strategy",
    Author = "MeepleAI")]
public sealed class RoutingLlmPlugin : RagPluginBase
{
    /// <inheritdoc />
    public override string Id => "routing-llm-v1";

    /// <inheritdoc />
    public override string Name => "LLM Routing";

    /// <inheritdoc />
    public override string Version => "1.0.0";

    /// <inheritdoc />
    public override PluginCategory Category => PluginCategory.Routing;

    /// <inheritdoc />
    protected override string Description => "Uses LLM to classify query intent and select processing strategy";

    /// <inheritdoc />
    protected override IReadOnlyList<string> Tags => ["routing", "llm", "intent", "classification"];

    /// <inheritdoc />
    protected override IReadOnlyList<string> Capabilities => ["intent-detection", "strategy-selection", "semantic-analysis"];

    private static readonly string[] ValidStrategies = ["FAST", "BALANCED", "PRECISE", "EXPERT", "CONSENSUS"];

    public RoutingLlmPlugin(ILogger<RoutingLlmPlugin> logger) : base(logger)
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

        // Perform LLM-based classification
        var classification = await ClassifyQueryAsync(query, customConfig, cancellationToken).ConfigureAwait(false);

        var result = JsonDocument.Parse(JsonSerializer.Serialize(new
        {
            strategy = classification.Strategy,
            confidence = classification.Confidence,
            queryType = classification.QueryType,
            reasoning = classification.Reasoning
        }));

        Logger.LogInformation(
            "Routing query via LLM: Strategy={Strategy}, Confidence={Confidence:F2}, QueryType={QueryType}",
            classification.Strategy, classification.Confidence, classification.QueryType);

        return PluginOutput.Successful(input.ExecutionId, result, classification.Confidence);
    }

    private async Task<RoutingClassification> ClassifyQueryAsync(
        string query,
        RoutingLlmConfig config,
        CancellationToken cancellationToken)
    {
        // Simulate LLM classification (in production, call actual LLM API)
        await Task.Delay(10, cancellationToken).ConfigureAwait(false); // Minimal delay for async compliance

        // Rule-based fallback classification (production would use actual LLM)
        var queryLower = query.ToLowerInvariant();

        // Determine query type
        var queryType = DetermineQueryType(queryLower);

        // Determine strategy based on query characteristics
        var (strategy, confidence, reasoning) = DetermineStrategy(queryLower, queryType);

        // Apply fallback if confidence is too low
        if (confidence < 0.5 && !string.IsNullOrEmpty(config.FallbackStrategy))
        {
            strategy = config.FallbackStrategy;
            reasoning = $"Low confidence ({confidence:F2}), using fallback strategy";
        }

        return new RoutingClassification(strategy, confidence, queryType, reasoning);
    }

    private static string DetermineQueryType(string query)
    {
        if (query.Contains("rule", StringComparison.Ordinal) || query.Contains("how to", StringComparison.Ordinal) || query.Contains("what happen", StringComparison.Ordinal))
            return "rules";
        if (query.Contains("resource", StringComparison.Ordinal) || query.Contains("component", StringComparison.Ordinal) || query.Contains("card", StringComparison.Ordinal) || query.Contains("piece", StringComparison.Ordinal))
            return "resources";
        if (query.Contains("strategy", StringComparison.Ordinal) || query.Contains("best way", StringComparison.Ordinal) || query.Contains("optimal", StringComparison.Ordinal) || query.Contains("win", StringComparison.Ordinal))
            return "strategy";
        if (query.Contains("setup", StringComparison.Ordinal) || query.Contains("start", StringComparison.Ordinal) || query.Contains("begin", StringComparison.Ordinal) || query.Contains("prepare", StringComparison.Ordinal))
            return "setup";
        if (query.Contains("learn", StringComparison.Ordinal) || query.Contains("teach", StringComparison.Ordinal) || query.Contains("explain", StringComparison.Ordinal) || query.Contains("understand", StringComparison.Ordinal))
            return "learning";

        return "unknown";
    }

    private static (string Strategy, double Confidence, string Reasoning) DetermineStrategy(string query, string queryType)
    {
        // Simple queries get FAST strategy
        if (query.Length < 50 && queryType is "rules" or "setup")
        {
            return ("FAST", 0.85, "Short, factual query about rules or setup");
        }

        // Complex strategy questions get EXPERT
        if (string.Equals(queryType, "strategy", StringComparison.Ordinal) && (query.Contains("complex", StringComparison.Ordinal) || query.Contains("advanced", StringComparison.Ordinal)))
        {
            return ("EXPERT", 0.78, "Complex strategy question requiring expert analysis");
        }

        // Learning queries get BALANCED with explanation focus
        if (string.Equals(queryType, "learning", StringComparison.Ordinal))
        {
            return ("BALANCED", 0.82, "Learning query requiring clear explanation");
        }

        // Ambiguous queries get PRECISE for accuracy
        if (string.Equals(queryType, "unknown", StringComparison.Ordinal) || query.Contains('?') && query.Length > 100)
        {
            return ("PRECISE", 0.65, "Ambiguous query requiring careful analysis");
        }

        // Default to BALANCED for most queries
        return ("BALANCED", 0.75, "Standard query, balanced approach recommended");
    }

    private static string GetQueryFromPayload(JsonDocument payload)
    {
        if (payload.RootElement.TryGetProperty("query", out var queryElement))
        {
            return queryElement.GetString() ?? string.Empty;
        }
        return string.Empty;
    }

    private static RoutingLlmConfig ParseCustomConfig(PluginConfig config)
    {
        if (config.CustomConfig == null)
        {
            return new RoutingLlmConfig();
        }

        var root = config.CustomConfig.RootElement;
        return new RoutingLlmConfig
        {
            Model = root.TryGetProperty("model", out var m) ? m.GetString() ?? "llama-3.3-70b" : "llama-3.3-70b",
            Temperature = root.TryGetProperty("temperature", out var t) ? t.GetDouble() : 0.1,
            FallbackStrategy = root.TryGetProperty("fallbackStrategy", out var f) ? f.GetString() ?? "BALANCED" : "BALANCED"
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

            if (root.TryGetProperty("temperature", out var temp))
            {
                var tempValue = temp.GetDouble();
                if (tempValue < 0 || tempValue > 2)
                {
                    errors.Add(new ValidationError
                    {
                        Message = "Temperature must be between 0 and 2",
                        PropertyPath = "customConfig.temperature",
                        Code = "INVALID_TEMPERATURE",
                        AttemptedValue = tempValue
                    });
                }
            }

            if (root.TryGetProperty("fallbackStrategy", out var fallback))
            {
                var fallbackValue = fallback.GetString();
                if (!string.IsNullOrEmpty(fallbackValue) && !ValidStrategies.Contains(fallbackValue, StringComparer.Ordinal))
                {
                    errors.Add(new ValidationError
                    {
                        Message = $"Invalid fallback strategy. Must be one of: {string.Join(", ", ValidStrategies)}",
                        PropertyPath = "customConfig.fallbackStrategy",
                        Code = "INVALID_FALLBACK_STRATEGY",
                        AttemptedValue = fallbackValue
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
            "description": "Input for LLM routing plugin",
            "properties": {
                "query": {
                    "type": "string",
                    "description": "The user query to classify"
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
            "description": "Output from LLM routing plugin",
            "properties": {
                "strategy": {
                    "type": "string",
                    "enum": ["FAST", "BALANCED", "PRECISE", "EXPERT", "CONSENSUS"],
                    "description": "Selected processing strategy"
                },
                "confidence": {
                    "type": "number",
                    "minimum": 0,
                    "maximum": 1,
                    "description": "Classification confidence"
                },
                "queryType": {
                    "type": "string",
                    "enum": ["rules", "resources", "strategy", "setup", "learning", "unknown"],
                    "description": "Classified query type"
                },
                "reasoning": {
                    "type": "string",
                    "description": "Explanation for the routing decision"
                }
            },
            "required": ["strategy", "confidence", "queryType", "reasoning"]
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
            "description": "Configuration for LLM routing plugin",
            "properties": {
                "model": {
                    "type": "string",
                    "description": "LLM model to use for classification",
                    "default": "llama-3.3-70b"
                },
                "temperature": {
                    "type": "number",
                    "minimum": 0,
                    "maximum": 2,
                    "description": "Classification temperature",
                    "default": 0.1
                },
                "fallbackStrategy": {
                    "type": "string",
                    "enum": ["FAST", "BALANCED", "PRECISE", "EXPERT", "CONSENSUS"],
                    "description": "Default strategy if classification fails",
                    "default": "BALANCED"
                }
            }
        }
        """);
    }

    private sealed record RoutingClassification(string Strategy, double Confidence, string QueryType, string Reasoning);

    private sealed class RoutingLlmConfig
    {
        public string Model { get; init; } = "llama-3.3-70b";
        public double Temperature { get; init; } = 0.1;
        public string FallbackStrategy { get; init; } = "BALANCED";
    }
}
