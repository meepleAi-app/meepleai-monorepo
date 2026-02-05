// =============================================================================
// MeepleAI - RAG Plugin System
// Issue #3418 - Routing Plugins
// =============================================================================

using System.Text.Json;
using System.Text.RegularExpressions;
using Api.BoundedContexts.KnowledgeBase.Domain.Plugins.Base;
using Api.BoundedContexts.KnowledgeBase.Domain.Plugins.Contracts;
using Api.BoundedContexts.KnowledgeBase.Domain.Plugins.Enums;
using Api.BoundedContexts.KnowledgeBase.Domain.Plugins.Models;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.KnowledgeBase.Domain.Plugins.Implementations.Routing;

/// <summary>
/// Hybrid routing plugin that combines rules and LLM for cost-effective routing.
/// Tries fast rule-based classification first, falls back to LLM for uncertain cases.
/// </summary>
[RagPlugin("routing-hybrid-v1",
    Category = PluginCategory.Routing,
    Name = "Hybrid Routing",
    Description = "Combines rule-based and LLM classification for cost-effective routing",
    Author = "MeepleAI",
    Priority = 75)]
public sealed class RoutingHybridPlugin : RagPluginBase
{
    /// <inheritdoc />
    public override string Id => "routing-hybrid-v1";

    /// <inheritdoc />
    public override string Name => "Hybrid Routing";

    /// <inheritdoc />
    public override string Version => "1.0.0";

    /// <inheritdoc />
    public override PluginCategory Category => PluginCategory.Routing;

    /// <inheritdoc />
    protected override string Description => "Combines rule-based and LLM classification for cost-effective routing";

    /// <inheritdoc />
    protected override IReadOnlyList<string> Tags => ["routing", "hybrid", "cost-effective", "balanced"];

    /// <inheritdoc />
    protected override IReadOnlyList<string> Capabilities => ["pattern-matching", "llm-fallback", "cost-optimization"];

    private static readonly List<HybridRule> DefaultRules =
    [
        // High-confidence rules (confidence >= 0.85)
        new("simple-what-is", @"^what\s+is\s+", "FAST", 0.90),
        new("simple-how-to", @"^how\s+(do|does|to)\s+", "FAST", 0.88),
        new("setup-direct", @"^(setup|set up|prepare)\s+", "FAST", 0.92),

        // Medium-confidence rules (confidence 0.70-0.84)
        new("rules-query", @"\b(rule|ruling)\b", "BALANCED", 0.80),
        new("strategy-query", @"\b(strategy|tactic)\b", "EXPERT", 0.75),
        new("timing-query", @"\b(timing|when|order)\b", "PRECISE", 0.78),

        // Low-confidence rules (confidence < 0.70) - trigger LLM fallback
        new("general-question", @"\?$", "BALANCED", 0.55),
        new("complex-query", @".{100,}", "PRECISE", 0.60)
    ];

    private const double LlmFallbackThreshold = 0.70;

    public RoutingHybridPlugin(ILogger<RoutingHybridPlugin> logger) : base(logger)
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

        // Try rules first if configured
        if (customConfig.RulesFirst)
        {
            var rulesResult = TryMatchRules(query, customConfig.Rules);

            if (rulesResult.Confidence >= LlmFallbackThreshold)
            {
                // High-confidence rule match, no need for LLM
                var rulesOutput = CreateOutput(
                    rulesResult.Strategy,
                    "rules",
                    rulesResult.Confidence,
                    rulesResult.MatchedRule);

                Logger.LogInformation(
                    "Hybrid routing: Rules matched with high confidence. Strategy={Strategy}, Rule={Rule}, Confidence={Confidence:F2}",
                    rulesResult.Strategy, rulesResult.MatchedRule ?? "none", rulesResult.Confidence);

                return PluginOutput.Successful(input.ExecutionId, rulesOutput, rulesResult.Confidence);
            }

            // Low-confidence match, try LLM if enabled
            if (customConfig.LlmFallback)
            {
                var llmResult = await ClassifyWithLlmAsync(query, cancellationToken).ConfigureAwait(false);

                var llmOutput = CreateOutput(
                    llmResult.Strategy,
                    "llm",
                    llmResult.Confidence,
                    null);

                Logger.LogInformation(
                    "Hybrid routing: LLM fallback used. Strategy={Strategy}, Confidence={Confidence:F2}",
                    llmResult.Strategy, llmResult.Confidence);

                return PluginOutput.Successful(input.ExecutionId, llmOutput, llmResult.Confidence);
            }

            // Return low-confidence rules result if LLM fallback is disabled
            var lowConfOutput = CreateOutput(
                rulesResult.Strategy,
                "rules",
                rulesResult.Confidence,
                rulesResult.MatchedRule);

            return PluginOutput.Successful(input.ExecutionId, lowConfOutput, rulesResult.Confidence);
        }
        else
        {
            // LLM first mode
            var llmResult = await ClassifyWithLlmAsync(query, cancellationToken).ConfigureAwait(false);

            var output = CreateOutput(
                llmResult.Strategy,
                "llm",
                llmResult.Confidence,
                null);

            Logger.LogInformation(
                "Hybrid routing: LLM-first mode. Strategy={Strategy}, Confidence={Confidence:F2}",
                llmResult.Strategy, llmResult.Confidence);

            return PluginOutput.Successful(input.ExecutionId, output, llmResult.Confidence);
        }
    }

    private static RuleMatchResult TryMatchRules(string query, IReadOnlyList<HybridRule> rules)
    {
        var queryLower = query.ToLowerInvariant();
        HybridRule? bestMatch = null;
        double highestConfidence = 0;

        foreach (var rule in rules)
        {
            try
            {
                if (Regex.IsMatch(queryLower, rule.Pattern, RegexOptions.IgnoreCase, TimeSpan.FromMilliseconds(100)))
                {
                    if (rule.Confidence > highestConfidence)
                    {
                        highestConfidence = rule.Confidence;
                        bestMatch = rule;
                    }
                }
            }
            catch (RegexMatchTimeoutException)
            {
                // Skip rules that timeout
            }
        }

        if (bestMatch != null)
        {
            return new RuleMatchResult(bestMatch.Strategy, bestMatch.Name, bestMatch.Confidence);
        }

        return new RuleMatchResult("BALANCED", null, 0.5);
    }

    private static async Task<LlmClassificationResult> ClassifyWithLlmAsync(
        string query,
        CancellationToken cancellationToken)
    {
        // Simulate LLM classification (production would call actual LLM)
        await Task.Delay(10, cancellationToken).ConfigureAwait(false);

        var queryLower = query.ToLowerInvariant();

        // Determine strategy based on query characteristics
        if (query.Length < 50)
        {
            return new LlmClassificationResult("FAST", 0.82);
        }

        if (queryLower.Contains("strategy") || queryLower.Contains("optimal"))
        {
            return new LlmClassificationResult("EXPERT", 0.78);
        }

        if (queryLower.Contains("complex") || queryLower.Contains("interaction"))
        {
            return new LlmClassificationResult("PRECISE", 0.75);
        }

        return new LlmClassificationResult("BALANCED", 0.72);
    }

    private static JsonDocument CreateOutput(string strategy, string method, double confidence, string? matchedRule)
    {
        var output = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["strategy"] = strategy,
            ["method"] = method,
            ["confidence"] = confidence
        };

        if (matchedRule != null)
        {
            output["matchedRule"] = matchedRule;
        }

        return JsonDocument.Parse(JsonSerializer.Serialize(output));
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
        var result = new HybridConfig();

        if (config.CustomConfig == null)
        {
            return result;
        }

        var root = config.CustomConfig.RootElement;

        if (root.TryGetProperty("rulesFirst", out var rulesFirst))
        {
            result.RulesFirst = rulesFirst.GetBoolean();
        }

        if (root.TryGetProperty("llmFallback", out var llmFallback))
        {
            result.LlmFallback = llmFallback.GetBoolean();
        }

        if (root.TryGetProperty("rules", out var rulesElement) && rulesElement.ValueKind == JsonValueKind.Array)
        {
            var customRules = new List<HybridRule>();
            foreach (var ruleElement in rulesElement.EnumerateArray())
            {
                var name = ruleElement.TryGetProperty("name", out var n) ? n.GetString() ?? "unnamed" : "unnamed";
                var pattern = ruleElement.TryGetProperty("pattern", out var p) ? p.GetString() ?? "" : "";
                var strategy = ruleElement.TryGetProperty("strategy", out var s) ? s.GetString() ?? "BALANCED" : "BALANCED";
                var confidence = ruleElement.TryGetProperty("confidence", out var c) ? c.GetDouble() : 0.75;

                if (!string.IsNullOrEmpty(pattern))
                {
                    customRules.Add(new HybridRule(name, pattern, strategy, confidence));
                }
            }

            if (customRules.Count > 0)
            {
                result.Rules = customRules;
            }
        }

        return result;
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
            "description": "Input for hybrid routing plugin",
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
            "description": "Output from hybrid routing plugin",
            "properties": {
                "strategy": {
                    "type": "string",
                    "enum": ["FAST", "BALANCED", "PRECISE", "EXPERT", "CONSENSUS"],
                    "description": "Selected processing strategy"
                },
                "method": {
                    "type": "string",
                    "enum": ["rules", "llm"],
                    "description": "Which classification method was used"
                },
                "confidence": {
                    "type": "number",
                    "minimum": 0,
                    "maximum": 1,
                    "description": "Classification confidence"
                },
                "matchedRule": {
                    "type": "string",
                    "description": "Name of matched rule (only when method is 'rules')"
                }
            },
            "required": ["strategy", "method", "confidence"]
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
            "description": "Configuration for hybrid routing plugin",
            "properties": {
                "rulesFirst": {
                    "type": "boolean",
                    "description": "Try rules before LLM",
                    "default": true
                },
                "llmFallback": {
                    "type": "boolean",
                    "description": "Use LLM if rules don't match with high confidence",
                    "default": true
                },
                "rules": {
                    "type": "array",
                    "description": "Custom routing rules",
                    "items": {
                        "type": "object",
                        "properties": {
                            "name": {
                                "type": "string"
                            },
                            "pattern": {
                                "type": "string"
                            },
                            "strategy": {
                                "type": "string",
                                "enum": ["FAST", "BALANCED", "PRECISE", "EXPERT", "CONSENSUS"]
                            },
                            "confidence": {
                                "type": "number",
                                "minimum": 0,
                                "maximum": 1
                            }
                        },
                        "required": ["pattern", "strategy"]
                    }
                }
            }
        }
        """);
    }

    private sealed record HybridRule(string Name, string Pattern, string Strategy, double Confidence);
    private sealed record RuleMatchResult(string Strategy, string? MatchedRule, double Confidence);
    private sealed record LlmClassificationResult(string Strategy, double Confidence);

    private sealed class HybridConfig
    {
        public bool RulesFirst { get; set; } = true;
        public bool LlmFallback { get; set; } = true;
        public IReadOnlyList<HybridRule> Rules { get; set; } = DefaultRules;
    }
}
