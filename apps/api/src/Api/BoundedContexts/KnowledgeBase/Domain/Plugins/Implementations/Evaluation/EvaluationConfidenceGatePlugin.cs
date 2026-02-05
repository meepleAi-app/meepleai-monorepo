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
/// Simple confidence threshold gate plugin.
/// Fast pass/fail evaluation based on confidence score.
/// </summary>
[RagPlugin("evaluation-confidence-gate-v1",
    Category = PluginCategory.Evaluation,
    Name = "Confidence Gate",
    Description = "Simple confidence threshold check for fast pass/fail decisions",
    Author = "MeepleAI",
    Priority = 50)]
public sealed class EvaluationConfidenceGatePlugin : RagPluginBase
{
    /// <inheritdoc />
    public override string Id => "evaluation-confidence-gate-v1";

    /// <inheritdoc />
    public override string Name => "Confidence Gate";

    /// <inheritdoc />
    public override string Version => "1.0.0";

    /// <inheritdoc />
    public override PluginCategory Category => PluginCategory.Evaluation;

    /// <inheritdoc />
    protected override string Description => "Simple confidence threshold check for fast pass/fail decisions";

    /// <inheritdoc />
    protected override IReadOnlyList<string> Tags => ["evaluation", "confidence", "gate", "fast"];

    /// <inheritdoc />
    protected override IReadOnlyList<string> Capabilities => ["threshold-check", "pass-fail"];

    public EvaluationConfidenceGatePlugin(ILogger<EvaluationConfidenceGatePlugin> logger) : base(logger)
    {
    }

    /// <inheritdoc />
    protected override Task<PluginOutput> ExecuteCoreAsync(
        PluginInput input,
        PluginConfig config,
        CancellationToken cancellationToken)
    {
        var confidence = GetConfidenceFromPayload(input.Payload);
        var customConfig = ParseCustomConfig(config);

        var passed = confidence >= customConfig.MinConfidence;
        var action = passed ? "proceed" : customConfig.FallbackAction;

        var result = JsonDocument.Parse(JsonSerializer.Serialize(new
        {
            passed = passed,
            confidence = confidence,
            action = action
        }));

        Logger.LogInformation(
            "Confidence gate: Confidence={Confidence:F2}, Threshold={Threshold:F2}, Passed={Passed}",
            confidence, customConfig.MinConfidence, passed);

        return Task.FromResult(PluginOutput.Successful(input.ExecutionId, result, confidence));
    }

    private static double GetConfidenceFromPayload(JsonDocument payload)
    {
        // Try multiple common locations for confidence score
        if (payload.RootElement.TryGetProperty("confidence", out var conf))
        {
            return conf.GetDouble();
        }

        if (payload.RootElement.TryGetProperty("score", out var score))
        {
            return score.GetDouble();
        }

        // Calculate from documents if available
        if (payload.RootElement.TryGetProperty("documents", out var docs) &&
            docs.ValueKind == JsonValueKind.Array)
        {
            var scores = new List<double>();
            foreach (var doc in docs.EnumerateArray())
            {
                if (doc.TryGetProperty("score", out var docScore))
                {
                    scores.Add(docScore.GetDouble());
                }
            }

            if (scores.Count > 0)
            {
                return scores.Max();
            }
        }

        // Default to 0 if no confidence found
        return 0;
    }

    private static ConfidenceGateConfig ParseCustomConfig(PluginConfig config)
    {
        if (config.CustomConfig == null)
        {
            return new ConfidenceGateConfig();
        }

        var root = config.CustomConfig.RootElement;
        return new ConfidenceGateConfig
        {
            MinConfidence = root.TryGetProperty("minConfidence", out var mc) ? mc.GetDouble() : 0.7,
            FallbackAction = root.TryGetProperty("fallbackAction", out var fa) ? fa.GetString() ?? "fallback" : "fallback"
        };
    }

    /// <inheritdoc />
    protected override ValidationResult ValidateConfigCore(PluginConfig config)
    {
        var errors = new List<ValidationError>();

        if (config.CustomConfig != null)
        {
            var root = config.CustomConfig.RootElement;

            if (root.TryGetProperty("minConfidence", out var mc))
            {
                var value = mc.GetDouble();
                if (value < 0 || value > 1)
                {
                    errors.Add(new ValidationError
                    {
                        Message = "minConfidence must be between 0 and 1",
                        PropertyPath = "customConfig.minConfidence",
                        Code = "INVALID_THRESHOLD",
                        AttemptedValue = value
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
            "description": "Input for confidence gate plugin",
            "properties": {
                "confidence": {
                    "type": "number",
                    "minimum": 0,
                    "maximum": 1,
                    "description": "Confidence score to evaluate"
                },
                "score": {
                    "type": "number",
                    "description": "Alternative field for confidence"
                },
                "documents": {
                    "type": "array",
                    "description": "Documents with scores (max score used)"
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
            "description": "Output from confidence gate plugin",
            "properties": {
                "passed": {
                    "type": "boolean",
                    "description": "Whether confidence met threshold"
                },
                "confidence": {
                    "type": "number",
                    "description": "The evaluated confidence score"
                },
                "action": {
                    "type": "string",
                    "enum": ["proceed", "fallback"],
                    "description": "Recommended action"
                }
            },
            "required": ["passed", "confidence", "action"]
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
            "description": "Configuration for confidence gate plugin",
            "properties": {
                "minConfidence": {
                    "type": "number",
                    "minimum": 0,
                    "maximum": 1,
                    "default": 0.7,
                    "description": "Minimum confidence to proceed"
                },
                "fallbackAction": {
                    "type": "string",
                    "default": "fallback",
                    "description": "Action when below threshold"
                }
            }
        }
        """);
    }

    private sealed class ConfidenceGateConfig
    {
        public double MinConfidence { get; init; } = 0.7;
        public string FallbackAction { get; init; } = "fallback";
    }
}
