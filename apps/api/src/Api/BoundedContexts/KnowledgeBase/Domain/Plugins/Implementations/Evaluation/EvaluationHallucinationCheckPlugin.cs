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
/// Hallucination detection plugin for context validation.
/// Identifies potential unsupported claims in generated content.
/// </summary>
[RagPlugin("evaluation-hallucination-check-v1",
    Category = PluginCategory.Evaluation,
    Name = "Hallucination Check",
    Description = "Detects potential hallucinations and unsupported claims in generated content",
    Author = "MeepleAI")]
public sealed class EvaluationHallucinationCheckPlugin : RagPluginBase
{
    /// <inheritdoc />
    public override string Id => "evaluation-hallucination-check-v1";

    /// <inheritdoc />
    public override string Name => "Hallucination Check";

    /// <inheritdoc />
    public override string Version => "1.0.0";

    /// <inheritdoc />
    public override PluginCategory Category => PluginCategory.Evaluation;

    /// <inheritdoc />
    protected override string Description => "Detects potential hallucinations and unsupported claims";

    /// <inheritdoc />
    protected override IReadOnlyList<string> Tags => ["evaluation", "hallucination", "quality", "verification"];

    /// <inheritdoc />
    protected override IReadOnlyList<string> Capabilities => ["hallucination-detection", "fact-checking", "grounding-verification"];

    public EvaluationHallucinationCheckPlugin(ILogger<EvaluationHallucinationCheckPlugin> logger) : base(logger)
    {
    }

    /// <inheritdoc />
    protected override async Task<PluginOutput> ExecuteCoreAsync(
        PluginInput input,
        PluginConfig config,
        CancellationToken cancellationToken)
    {
        var (response, context) = ParsePayload(input.Payload);

        if (string.IsNullOrWhiteSpace(response))
        {
            return PluginOutput.Failed(input.ExecutionId, "Response text is required", "MISSING_RESPONSE");
        }

        var customConfig = ParseCustomConfig(config);

        // Check for potential hallucinations
        var checkResult = await CheckForHallucinationsAsync(response, context, customConfig, cancellationToken).ConfigureAwait(false);

        var result = JsonDocument.Parse(JsonSerializer.Serialize(new
        {
            hasPotentialIssues = checkResult.HasIssues,
            flaggedContent = checkResult.FlaggedContent,
            confidence = checkResult.Confidence
        }));

        Logger.LogInformation(
            "Hallucination check: HasIssues={HasIssues}, FlaggedCount={FlaggedCount}, Confidence={Confidence:F2}",
            checkResult.HasIssues, checkResult.FlaggedContent.Count, checkResult.Confidence);

        return PluginOutput.Successful(input.ExecutionId, result, checkResult.Confidence);
    }

    private static async Task<HallucinationCheckResult> CheckForHallucinationsAsync(
        string response,
        List<string> context,
        HallucinationConfig config,
        CancellationToken cancellationToken)
    {
        // Simulate LLM-based hallucination detection
        await Task.Delay(25, cancellationToken).ConfigureAwait(false);

        var flaggedContent = new List<string>();
        var contextText = string.Join(" ", context).ToLowerInvariant();
        var sentences = response.Split(['.', '!', '?'], StringSplitOptions.RemoveEmptyEntries);

        foreach (var sentence in sentences)
        {
            var trimmed = sentence.Trim();
            if (string.IsNullOrEmpty(trimmed) || trimmed.Length < 10)
            {
                continue;
            }

            // Check if key terms in the sentence are grounded in context
            var words = trimmed.ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var significantWords = words.Where(w => w.Length > 4).ToList();

            if (significantWords.Count > 0)
            {
                var groundedCount = significantWords.Count(w => contextText.Contains(w, StringComparison.Ordinal));
                var groundingRatio = (double)groundedCount / significantWords.Count;

                // In strict mode, require higher grounding
                var threshold = config.StrictMode ? 0.5 : 0.3;

                if (groundingRatio < threshold)
                {
                    flaggedContent.Add(trimmed);
                }
            }
        }

        var hasIssues = flaggedContent.Count > 0;
        var confidence = hasIssues
            ? 1.0 - ((double)flaggedContent.Count / Math.Max(sentences.Length, 1))
            : 0.95;

        return new HallucinationCheckResult(hasIssues, flaggedContent, confidence);
    }

    private static (string Response, List<string> Context) ParsePayload(JsonDocument payload)
    {
        var response = string.Empty;
        var context = new List<string>();

        if (payload.RootElement.TryGetProperty("response", out var respElement))
        {
            response = respElement.GetString() ?? string.Empty;
        }

        if (payload.RootElement.TryGetProperty("context", out var ctxElement))
        {
            if (ctxElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in ctxElement.EnumerateArray())
                {
                    var text = item.ValueKind == JsonValueKind.String
                        ? item.GetString()
                        : item.TryGetProperty("content", out var c) ? c.GetString() : null;

                    if (!string.IsNullOrEmpty(text))
                    {
                        context.Add(text);
                    }
                }
            }
            else if (ctxElement.ValueKind == JsonValueKind.String)
            {
                context.Add(ctxElement.GetString() ?? string.Empty);
            }
        }

        // Also check for documents array
        if (payload.RootElement.TryGetProperty("documents", out var docsElement) &&
            docsElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var doc in docsElement.EnumerateArray())
            {
                if (doc.TryGetProperty("content", out var content))
                {
                    var text = content.GetString();
                    if (!string.IsNullOrEmpty(text))
                    {
                        context.Add(text);
                    }
                }
            }
        }

        return (response, context);
    }

    private static HallucinationConfig ParseCustomConfig(PluginConfig config)
    {
        if (config.CustomConfig == null)
        {
            return new HallucinationConfig();
        }

        var root = config.CustomConfig.RootElement;
        return new HallucinationConfig
        {
            Model = root.TryGetProperty("model", out var m) ? m.GetString() ?? "default" : "default",
            StrictMode = root.TryGetProperty("strictMode", out var s) && s.GetBoolean()
        };
    }

    /// <inheritdoc />
    protected override ValidationResult ValidateInputCore(PluginInput input)
    {
        var errors = new List<ValidationError>();

        if (!input.Payload.RootElement.TryGetProperty("response", out var respElement) ||
            string.IsNullOrWhiteSpace(respElement.GetString()))
        {
            errors.Add(new ValidationError
            {
                Message = "Response text is required in payload",
                PropertyPath = "payload.response",
                Code = "MISSING_RESPONSE"
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
            "description": "Input for hallucination check plugin",
            "properties": {
                "response": {
                    "type": "string",
                    "description": "The generated response to check"
                },
                "context": {
                    "oneOf": [
                        { "type": "string" },
                        { "type": "array", "items": { "type": "string" } }
                    ],
                    "description": "Source context for grounding verification"
                },
                "documents": {
                    "type": "array",
                    "description": "Source documents (alternative to context)",
                    "items": {
                        "type": "object",
                        "properties": {
                            "content": { "type": "string" }
                        }
                    }
                }
            },
            "required": ["response"]
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
            "description": "Output from hallucination check plugin",
            "properties": {
                "hasPotentialIssues": {
                    "type": "boolean",
                    "description": "Whether potential hallucinations were detected"
                },
                "flaggedContent": {
                    "type": "array",
                    "items": { "type": "string" },
                    "description": "Content segments flagged as potentially hallucinated"
                },
                "confidence": {
                    "type": "number",
                    "minimum": 0,
                    "maximum": 1,
                    "description": "Confidence in the check result"
                }
            },
            "required": ["hasPotentialIssues", "flaggedContent", "confidence"]
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
            "description": "Configuration for hallucination check plugin",
            "properties": {
                "model": {
                    "type": "string",
                    "description": "Model for hallucination detection"
                },
                "strictMode": {
                    "type": "boolean",
                    "default": false,
                    "description": "Use stricter grounding requirements"
                }
            }
        }
        """);
    }

    private sealed record HallucinationCheckResult(bool HasIssues, List<string> FlaggedContent, double Confidence);

    private sealed class HallucinationConfig
    {
        public string Model { get; init; } = "default";
        public bool StrictMode { get; init; }
    }
}
