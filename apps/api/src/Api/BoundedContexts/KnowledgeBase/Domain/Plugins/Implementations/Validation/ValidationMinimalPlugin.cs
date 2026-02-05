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
/// Minimal validation plugin for fast safety and format checks.
/// Quick pass/fail validation without deep analysis.
/// </summary>
[RagPlugin("validation-minimal-v1",
    Category = PluginCategory.Validation,
    Name = "Minimal Validation",
    Description = "Quick safety and format check for fast validation",
    Author = "MeepleAI",
    Priority = 50)]
public sealed class ValidationMinimalPlugin : RagPluginBase
{
    /// <inheritdoc />
    public override string Id => "validation-minimal-v1";

    /// <inheritdoc />
    public override string Name => "Minimal Validation";

    /// <inheritdoc />
    public override string Version => "1.0.0";

    /// <inheritdoc />
    public override PluginCategory Category => PluginCategory.Validation;

    /// <inheritdoc />
    protected override string Description => "Quick safety and format check";

    /// <inheritdoc />
    protected override IReadOnlyList<string> Tags => ["validation", "minimal", "fast", "safety"];

    /// <inheritdoc />
    protected override IReadOnlyList<string> Capabilities => ["safety-check", "format-check", "fast-validation"];

    // Basic safety patterns
    private static readonly string[] UnsafePatterns =
    [
        "password", "secret", "api_key", "private_key", "credit_card",
        "ssn", "social_security"
    ];

    public ValidationMinimalPlugin(ILogger<ValidationMinimalPlugin> logger) : base(logger)
    {
    }

    /// <inheritdoc />
    protected override Task<PluginOutput> ExecuteCoreAsync(
        PluginInput input,
        PluginConfig config,
        CancellationToken cancellationToken)
    {
        var response = GetResponseFromPayload(input.Payload);

        if (string.IsNullOrWhiteSpace(response))
        {
            return Task.FromResult(PluginOutput.Failed(input.ExecutionId, "Response is required", "MISSING_RESPONSE"));
        }

        var customConfig = ParseCustomConfig(config);

        var safetyPassed = true;
        var formatValid = true;

        // Safety check
        if (customConfig.ShouldCheckSafety)
        {
            safetyPassed = CheckSafety(response);
        }

        // Format check
        if (customConfig.ShouldCheckFormat && !string.IsNullOrEmpty(customConfig.ExpectedFormat))
        {
            formatValid = CheckFormat(response, customConfig.ExpectedFormat);
        }

        var passed = safetyPassed && formatValid;

        var result = JsonDocument.Parse(JsonSerializer.Serialize(new
        {
            passed = passed,
            safetyPassed = safetyPassed,
            formatValid = formatValid
        }));

        Logger.LogInformation(
            "Minimal validation: Passed={Passed}, Safety={Safety}, Format={Format}",
            passed, safetyPassed, formatValid);

        return Task.FromResult(PluginOutput.Successful(input.ExecutionId, result, passed ? 1.0 : 0.5));
    }

    private static bool CheckSafety(string response)
    {
        var responseLower = response.ToLowerInvariant();
        return !UnsafePatterns.Any(pattern =>
            responseLower.Contains(pattern, StringComparison.Ordinal));
    }

    private static bool CheckFormat(string response, string expectedFormat)
    {
        return expectedFormat switch
        {
            "json" => IsValidJson(response),
            "markdown" => IsValidMarkdown(response),
            "plain" => IsValidPlainText(response),
            _ => true
        };
    }

    private static bool IsValidJson(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        text = text.Trim();
        if (!((text.StartsWith('{') && text.EndsWith('}')) ||
              (text.StartsWith('[') && text.EndsWith(']'))))
        {
            return false;
        }

        try
        {
            using var doc = JsonDocument.Parse(text);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool IsValidMarkdown(string text)
    {
        // Basic markdown validation - just check it's not empty
        // and doesn't contain obvious errors
        return !string.IsNullOrWhiteSpace(text);
    }

    private static bool IsValidPlainText(string text)
    {
        // Plain text is always valid if not empty
        return !string.IsNullOrWhiteSpace(text);
    }

    private static string GetResponseFromPayload(JsonDocument payload)
    {
        if (payload.RootElement.TryGetProperty("response", out var respElement))
        {
            return respElement.GetString() ?? string.Empty;
        }
        return string.Empty;
    }

    private static MinimalConfig ParseCustomConfig(PluginConfig config)
    {
        if (config.CustomConfig == null)
        {
            return new MinimalConfig();
        }

        var root = config.CustomConfig.RootElement;
        return new MinimalConfig
        {
            ShouldCheckSafety = !root.TryGetProperty("checkSafety", out var cs) || cs.GetBoolean(),
            ShouldCheckFormat = root.TryGetProperty("checkFormat", out var cf) && cf.GetBoolean(),
            ExpectedFormat = root.TryGetProperty("expectedFormat", out var ef) ? ef.GetString() : null
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
                "response": { "type": "string" }
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
                "safetyPassed": { "type": "boolean" },
                "formatValid": { "type": "boolean" }
            },
            "required": ["passed", "safetyPassed", "formatValid"]
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
                "checkSafety": { "type": "boolean", "default": true },
                "checkFormat": { "type": "boolean", "default": false },
                "expectedFormat": {
                    "type": "string",
                    "enum": ["json", "markdown", "plain"]
                }
            }
        }
        """);
    }

    private sealed class MinimalConfig
    {
        public bool ShouldCheckSafety { get; init; } = true;
        public bool ShouldCheckFormat { get; init; }
        public string? ExpectedFormat { get; init; }
    }
}
