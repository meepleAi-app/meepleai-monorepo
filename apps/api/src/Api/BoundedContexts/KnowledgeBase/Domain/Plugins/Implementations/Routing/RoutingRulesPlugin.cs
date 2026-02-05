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
/// Rule-based routing plugin that classifies queries using keyword patterns.
/// Fast, deterministic classification without LLM dependencies.
/// </summary>
[RagPlugin("routing-rules-v1",
    Category = PluginCategory.Routing,
    Name = "Rules Routing",
    Description = "Rule-based classification using keyword patterns for fast, deterministic routing",
    Author = "MeepleAI",
    Priority = 50)]
public sealed class RoutingRulesPlugin : RagPluginBase
{
    /// <inheritdoc />
    public override string Id => "routing-rules-v1";

    /// <inheritdoc />
    public override string Name => "Rules Routing";

    /// <inheritdoc />
    public override string Version => "1.0.0";

    /// <inheritdoc />
    public override PluginCategory Category => PluginCategory.Routing;

    /// <inheritdoc />
    protected override string Description => "Rule-based classification using keyword patterns";

    /// <inheritdoc />
    protected override IReadOnlyList<string> Tags => ["routing", "rules", "fast", "deterministic"];

    /// <inheritdoc />
    protected override IReadOnlyList<string> Capabilities => ["pattern-matching", "keyword-classification"];

    private static readonly List<RoutingRule> DefaultRules =
    [
        // FAST strategy rules
        new("quick-rules", @"\b(what|how)\s+(is|are|do|does)\b", "FAST", 100),
        new("setup-query", @"\b(setup|set up|prepare|start|begin)\b", "FAST", 90),
        new("component-query", @"\b(card|piece|token|die|dice|board)\b", "FAST", 85),

        // BALANCED strategy rules
        new("general-rules", @"\b(rule|ruling|allowed|can i|may i)\b", "BALANCED", 80),
        new("turn-order", @"\b(turn|phase|round|action)\b", "BALANCED", 75),
        new("resource-query", @"\b(resource|gold|money|point|score)\b", "BALANCED", 70),

        // PRECISE strategy rules
        new("complex-interaction", @"\b(interaction|combine|stack|trigger)\b", "PRECISE", 85),
        new("edge-case", @"\b(edge case|exception|special|rare)\b", "PRECISE", 90),
        new("timing-query", @"\b(timing|when|order|sequence|priority)\b", "PRECISE", 80),

        // EXPERT strategy rules
        new("strategy-advanced", @"\b(strategy|optimal|best|meta|competitive)\b", "EXPERT", 85),
        new("win-condition", @"\b(win|victory|defeat|end game)\b", "EXPERT", 75),
        new("advanced-play", @"\b(advanced|expert|pro|high level)\b", "EXPERT", 90),

        // CONSENSUS strategy rules
        new("opinion-query", @"\b(opinion|think|feel|prefer|recommend)\b", "CONSENSUS", 80),
        new("comparison", @"\b(compare|versus|vs|better|worse)\b", "CONSENSUS", 75),
        new("debate-topic", @"\b(debate|discuss|argument|controversial)\b", "CONSENSUS", 85)
    ];

    public RoutingRulesPlugin(ILogger<RoutingRulesPlugin> logger) : base(logger)
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

        var rules = GetRulesFromConfig(config);
        var matchResult = MatchRules(query, rules);

        var result = JsonDocument.Parse(JsonSerializer.Serialize(new
        {
            strategy = matchResult.Strategy,
            matchedRule = matchResult.MatchedRule,
            confidence = matchResult.Confidence
        }));

        Logger.LogInformation(
            "Routing query via rules: Strategy={Strategy}, MatchedRule={MatchedRule}, Confidence={Confidence:F2}",
            matchResult.Strategy, matchResult.MatchedRule ?? "none", matchResult.Confidence);

        return Task.FromResult(PluginOutput.Successful(input.ExecutionId, result, matchResult.Confidence));
    }

    private static RuleMatchResult MatchRules(string query, IReadOnlyList<RoutingRule> rules)
    {
        var queryLower = query.ToLowerInvariant();
        RoutingRule? bestMatch = null;
        int highestPriority = -1;

        foreach (var rule in rules)
        {
            try
            {
                if (Regex.IsMatch(queryLower, rule.Pattern, RegexOptions.IgnoreCase, TimeSpan.FromMilliseconds(100)))
                {
                    if (rule.Priority > highestPriority)
                    {
                        highestPriority = rule.Priority;
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
            return new RuleMatchResult(bestMatch.Strategy, bestMatch.Name, 1.0);
        }

        // No match - return default BALANCED strategy with lower confidence
        return new RuleMatchResult("BALANCED", null, 0.5);
    }

    private static IReadOnlyList<RoutingRule> GetRulesFromConfig(PluginConfig config)
    {
        if (config.CustomConfig == null)
        {
            return DefaultRules;
        }

        var root = config.CustomConfig.RootElement;
        if (!root.TryGetProperty("rules", out var rulesElement) || rulesElement.ValueKind != JsonValueKind.Array)
        {
            return DefaultRules;
        }

        var customRules = new List<RoutingRule>();
        foreach (var ruleElement in rulesElement.EnumerateArray())
        {
            var name = ruleElement.TryGetProperty("name", out var n) ? n.GetString() ?? "unnamed" : "unnamed";
            var pattern = ruleElement.TryGetProperty("pattern", out var p) ? p.GetString() ?? "" : "";
            var strategy = ruleElement.TryGetProperty("strategy", out var s) ? s.GetString() ?? "BALANCED" : "BALANCED";
            var priority = ruleElement.TryGetProperty("priority", out var pr) ? pr.GetInt32() : 50;

            if (!string.IsNullOrEmpty(pattern))
            {
                customRules.Add(new RoutingRule(name, pattern, strategy, priority));
            }
        }

        return customRules.Count > 0 ? customRules : DefaultRules;
    }

    private static string GetQueryFromPayload(JsonDocument payload)
    {
        if (payload.RootElement.TryGetProperty("query", out var queryElement))
        {
            return queryElement.GetString() ?? string.Empty;
        }
        return string.Empty;
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

            if (root.TryGetProperty("rules", out var rulesElement))
            {
                if (rulesElement.ValueKind != JsonValueKind.Array)
                {
                    errors.Add(new ValidationError
                    {
                        Message = "Rules must be an array",
                        PropertyPath = "customConfig.rules",
                        Code = "INVALID_RULES_FORMAT"
                    });
                }
                else
                {
                    int index = 0;
                    foreach (var rule in rulesElement.EnumerateArray())
                    {
                        if (!rule.TryGetProperty("pattern", out _))
                        {
                            errors.Add(new ValidationError
                            {
                                Message = $"Rule at index {index} is missing required 'pattern' property",
                                PropertyPath = $"customConfig.rules[{index}].pattern",
                                Code = "MISSING_PATTERN"
                            });
                        }
                        index++;
                    }
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
            "description": "Input for rules routing plugin",
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
            "description": "Output from rules routing plugin",
            "properties": {
                "strategy": {
                    "type": "string",
                    "enum": ["FAST", "BALANCED", "PRECISE", "EXPERT", "CONSENSUS"],
                    "description": "Selected processing strategy"
                },
                "matchedRule": {
                    "type": ["string", "null"],
                    "description": "Name of the matched rule, or null if no match"
                },
                "confidence": {
                    "type": "number",
                    "minimum": 0,
                    "maximum": 1,
                    "description": "Always 1.0 for exact matches, 0.5 for no match"
                }
            },
            "required": ["strategy", "confidence"]
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
            "description": "Configuration for rules routing plugin",
            "properties": {
                "rules": {
                    "type": "array",
                    "description": "Custom routing rules",
                    "items": {
                        "type": "object",
                        "properties": {
                            "name": {
                                "type": "string",
                                "description": "Rule identifier"
                            },
                            "pattern": {
                                "type": "string",
                                "description": "Regex pattern to match"
                            },
                            "strategy": {
                                "type": "string",
                                "enum": ["FAST", "BALANCED", "PRECISE", "EXPERT", "CONSENSUS"],
                                "description": "Strategy to use when pattern matches"
                            },
                            "priority": {
                                "type": "integer",
                                "minimum": 0,
                                "maximum": 100,
                                "description": "Rule priority (higher = more important)"
                            }
                        },
                        "required": ["pattern", "strategy"]
                    }
                }
            }
        }
        """);
    }

    private sealed record RoutingRule(string Name, string Pattern, string Strategy, int Priority);
    private sealed record RuleMatchResult(string Strategy, string? MatchedRule, double Confidence);
}
