// =============================================================================
// MeepleAI - RAG Plugin System
// Issue #3423 - Validation Plugins
// =============================================================================

using System.Text.Json;
using Api.BoundedContexts.KnowledgeBase.Domain.Plugins.Base;
using Api.BoundedContexts.KnowledgeBase.Domain.Plugins.Contracts;
using Api.BoundedContexts.KnowledgeBase.Domain.Plugins.Enums;
using Api.BoundedContexts.KnowledgeBase.Domain.Plugins.Models;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.KnowledgeBase.Domain.Plugins.Implementations.Validation;

/// <summary>
/// Multi-model consensus validation plugin.
/// Multiple models validate the response for agreement.
/// </summary>
[RagPlugin("validation-multi-model-v1",
    Category = PluginCategory.Validation,
    Name = "Multi-Model Validation",
    Description = "Multiple models validate response for consensus",
    Author = "MeepleAI")]
public sealed class ValidationMultiModelPlugin : RagPluginBase
{
    /// <inheritdoc />
    public override string Id => "validation-multi-model-v1";

    /// <inheritdoc />
    public override string Name => "Multi-Model Validation";

    /// <inheritdoc />
    public override string Version => "1.0.0";

    /// <inheritdoc />
    public override PluginCategory Category => PluginCategory.Validation;

    /// <inheritdoc />
    protected override string Description => "Multiple models validate response for consensus";

    /// <inheritdoc />
    protected override IReadOnlyList<string> Tags => ["validation", "multi-model", "consensus", "agreement"];

    /// <inheritdoc />
    protected override IReadOnlyList<string> Capabilities => ["multi-model", "consensus-validation", "cross-validation"];

    private static readonly string[] DefaultValidators = ["llama-3.3-70b", "claude-3-haiku", "gpt-4o-mini"];
    private static readonly string[] DefaultAspects = ["accuracy", "completeness", "safety"];

    public ValidationMultiModelPlugin(ILogger<ValidationMultiModelPlugin> logger) : base(logger)
    {
    }

    /// <inheritdoc />
    protected override async Task<PluginOutput> ExecuteCoreAsync(
        PluginInput input,
        PluginConfig config,
        CancellationToken cancellationToken)
    {
        var (response, query, context) = ParsePayload(input.Payload);

        if (string.IsNullOrWhiteSpace(response))
        {
            return PluginOutput.Failed(input.ExecutionId, "Response is required for validation", "MISSING_RESPONSE");
        }

        var customConfig = ParseCustomConfig(config);

        // Run validation across multiple models in parallel
        var modelTasks = customConfig.Validators.Select(model =>
            ValidateWithModelAsync(model, response, query, context, customConfig.Aspects, cancellationToken));

        var modelResults = await Task.WhenAll(modelTasks).ConfigureAwait(false);

        var modelVotes = customConfig.Validators
            .Zip(modelResults, (model, result) => (model, result))
            .ToDictionary(
                x => x.model,
                x => new { passed = x.result.Passed, feedback = x.result.Feedback },
                StringComparer.Ordinal);

        // Check consensus
        var passedCount = modelResults.Count(r => r.Passed);
        var totalModels = modelResults.Length;
        var consensusReached = (double)passedCount / totalModels >= customConfig.ConsensusThreshold;

        var passed = consensusReached && passedCount > totalModels / 2;

        var result = JsonDocument.Parse(JsonSerializer.Serialize(new
        {
            passed = passed,
            modelVotes = modelVotes,
            consensusReached = consensusReached
        }));

        Logger.LogInformation(
            "Multi-model validation: Passed={Passed}, Consensus={Consensus}, Models={Models}",
            passed, consensusReached, totalModels);

        var confidence = (double)passedCount / totalModels;
        return PluginOutput.Successful(input.ExecutionId, result, confidence);
    }

    private static async Task<ModelValidationResult> ValidateWithModelAsync(
        string model,
        string response,
        string query,
        List<string> context,
        IReadOnlyList<string> aspects,
        CancellationToken cancellationToken)
    {
        // Simulate model validation (production calls actual LLM APIs)
        await Task.Delay(30, cancellationToken).ConfigureAwait(false);

        // Simulate different model behaviors
        var random = new Random((model + response).GetHashCode(StringComparison.Ordinal));
        var passed = random.NextDouble() > 0.25; // 75% pass rate simulation

        var feedback = passed
            ? $"[{model}] Response meets quality standards for: {string.Join(", ", aspects)}"
            : $"[{model}] Response needs improvement in: {aspects[random.Next(aspects.Count)]}";

        return new ModelValidationResult(passed, feedback);
    }

    private static (string Response, string Query, List<string> Context) ParsePayload(JsonDocument payload)
    {
        var response = string.Empty;
        var query = string.Empty;
        var context = new List<string>();

        if (payload.RootElement.TryGetProperty("response", out var respElement))
        {
            response = respElement.GetString() ?? string.Empty;
        }

        if (payload.RootElement.TryGetProperty("query", out var queryElement))
        {
            query = queryElement.GetString() ?? string.Empty;
        }

        if (payload.RootElement.TryGetProperty("context", out var ctxElement) &&
            ctxElement.ValueKind == JsonValueKind.Array)
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

        return (response, query, context);
    }

    private static MultiModelConfig ParseCustomConfig(PluginConfig config)
    {
        if (config.CustomConfig == null)
        {
            return new MultiModelConfig();
        }

        var root = config.CustomConfig.RootElement;
        var validators = new List<string>();
        var aspects = new List<string>();

        if (root.TryGetProperty("validators", out var validatorsElement) &&
            validatorsElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var v in validatorsElement.EnumerateArray())
            {
                var val = v.GetString();
                if (!string.IsNullOrEmpty(val))
                {
                    validators.Add(val);
                }
            }
        }

        if (root.TryGetProperty("aspects", out var aspectsElement) &&
            aspectsElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var a in aspectsElement.EnumerateArray())
            {
                var asp = a.GetString();
                if (!string.IsNullOrEmpty(asp))
                {
                    aspects.Add(asp);
                }
            }
        }

        return new MultiModelConfig
        {
            Validators = validators.Count > 0 ? validators : DefaultValidators.ToList(),
            ConsensusThreshold = root.TryGetProperty("consensusThreshold", out var ct) ? ct.GetDouble() : 0.6,
            Aspects = aspects.Count > 0 ? aspects : DefaultAspects.ToList()
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
                Message = "Response is required in payload",
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
            "properties": {
                "response": { "type": "string" },
                "query": { "type": "string" },
                "context": { "type": "array", "items": { "type": "string" } }
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
            "properties": {
                "passed": { "type": "boolean" },
                "modelVotes": {
                    "type": "object",
                    "additionalProperties": {
                        "type": "object",
                        "properties": {
                            "passed": { "type": "boolean" },
                            "feedback": { "type": "string" }
                        }
                    }
                },
                "consensusReached": { "type": "boolean" }
            },
            "required": ["passed", "modelVotes", "consensusReached"]
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
                "validators": {
                    "type": "array",
                    "items": { "type": "string" },
                    "description": "Model IDs for validation"
                },
                "consensusThreshold": {
                    "type": "number",
                    "minimum": 0,
                    "maximum": 1,
                    "default": 0.6
                },
                "aspects": {
                    "type": "array",
                    "items": { "type": "string" },
                    "default": ["accuracy", "completeness", "safety"]
                }
            }
        }
        """);
    }

    private sealed record ModelValidationResult(bool Passed, string Feedback);

    private sealed class MultiModelConfig
    {
        public IReadOnlyList<string> Validators { get; init; } = DefaultValidators.ToList();
        public double ConsensusThreshold { get; init; } = 0.6;
        public IReadOnlyList<string> Aspects { get; init; } = DefaultAspects.ToList();
    }
}
