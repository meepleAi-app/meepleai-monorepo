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
/// Standard quality validation plugin.
/// Performs relevance, coherence, safety, and factuality checks.
/// </summary>
[RagPlugin("validation-standard-v1",
    Category = PluginCategory.Validation,
    Name = "Standard Validation",
    Description = "Standard quality checks: relevance, coherence, safety, factuality",
    Author = "MeepleAI")]
public sealed class ValidationStandardPlugin : RagPluginBase
{
    /// <inheritdoc />
    public override string Id => "validation-standard-v1";

    /// <inheritdoc />
    public override string Name => "Standard Validation";

    /// <inheritdoc />
    public override string Version => "1.0.0";

    /// <inheritdoc />
    public override PluginCategory Category => PluginCategory.Validation;

    /// <inheritdoc />
    protected override string Description => "Standard quality checks: relevance, coherence, safety, factuality";

    /// <inheritdoc />
    protected override IReadOnlyList<string> Tags => ["validation", "quality", "safety", "coherence"];

    /// <inheritdoc />
    protected override IReadOnlyList<string> Capabilities => ["relevance-check", "coherence-check", "safety-check", "factuality-check"];

    private static readonly string[] DefaultChecks = ["relevance", "coherence", "safety", "factuality"];

    // Simple safety patterns (production uses more sophisticated filters)
    private static readonly string[] UnsafePatterns =
    [
        "password", "credit card", "social security", "private key",
        "hack", "exploit", "malware", "illegal"
    ];

    public ValidationStandardPlugin(ILogger<ValidationStandardPlugin> logger) : base(logger)
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

        // Run all configured checks
        var checkResults = await RunChecksAsync(response, query, context, customConfig, cancellationToken).ConfigureAwait(false);

        // Calculate overall score
        var overallScore = checkResults.Values.Average(r => r.Score);
        var passed = customConfig.StrictMode
            ? checkResults.Values.All(r => r.Passed)
            : overallScore >= customConfig.MinScore;

        // Collect suggestions
        var suggestions = checkResults
            .Where(kvp => !kvp.Value.Passed && kvp.Value.Issues.Count > 0)
            .SelectMany(kvp => kvp.Value.Issues.Select(i => $"[{kvp.Key}] {i}"))
            .ToList();

        var result = JsonDocument.Parse(JsonSerializer.Serialize(new
        {
            passed = passed,
            overallScore = overallScore,
            checkResults = checkResults.ToDictionary(
                kvp => kvp.Key,
                kvp => new
                {
                    passed = kvp.Value.Passed,
                    score = kvp.Value.Score,
                    issues = kvp.Value.Issues
                },
                StringComparer.Ordinal),
            suggestions = suggestions
        }));

        Logger.LogInformation(
            "Standard validation: Passed={Passed}, Score={Score:F2}, Checks={Checks}",
            passed, overallScore, checkResults.Count);

        return PluginOutput.Successful(input.ExecutionId, result, overallScore);
    }

    private static async Task<Dictionary<string, CheckResult>> RunChecksAsync(
        string response,
        string query,
        List<string> context,
        StandardValidationConfig config,
        CancellationToken cancellationToken)
    {
        var results = new Dictionary<string, CheckResult>(StringComparer.Ordinal);

        foreach (var check in config.Checks)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var checkResult = check switch
            {
                "relevance" => await CheckRelevanceAsync(response, query, context, cancellationToken).ConfigureAwait(false),
                "coherence" => await CheckCoherenceAsync(response, cancellationToken).ConfigureAwait(false),
                "safety" => CheckSafety(response),
                "factuality" => await CheckFactualityAsync(response, context, cancellationToken).ConfigureAwait(false),
                _ => new CheckResult(true, 1.0, [])
            };

            results[check] = checkResult;
        }

        return results;
    }

    private static async Task<CheckResult> CheckRelevanceAsync(
        string response,
        string query,
        List<string> context,
        CancellationToken cancellationToken)
    {
        await Task.Delay(10, cancellationToken).ConfigureAwait(false);

        var issues = new List<string>();

        // Check if response addresses the query
        if (string.IsNullOrEmpty(query))
        {
            return new CheckResult(true, 0.8, []);
        }

        var queryTerms = query.ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(t => t.Length > 3).ToList();

        var responseLower = response.ToLowerInvariant();
        var matchedTerms = queryTerms.Count(t => responseLower.Contains(t, StringComparison.Ordinal));

        var relevanceScore = queryTerms.Count > 0
            ? (double)matchedTerms / queryTerms.Count
            : 0.8;

        if (relevanceScore < 0.3)
        {
            issues.Add("Response may not fully address the query");
        }

        // Adjust score
        relevanceScore = Math.Max(0.5, relevanceScore);

        return new CheckResult(relevanceScore >= 0.6, relevanceScore, issues);
    }

    private static async Task<CheckResult> CheckCoherenceAsync(
        string response,
        CancellationToken cancellationToken)
    {
        await Task.Delay(10, cancellationToken).ConfigureAwait(false);

        var issues = new List<string>();
        var score = 1.0;

        // Check for basic coherence issues
        if (response.Length < 20)
        {
            issues.Add("Response may be too brief");
            score -= 0.2;
        }

        // Check for repeated content
        var sentences = response.Split(['.', '!', '?'], StringSplitOptions.RemoveEmptyEntries);
        var uniqueSentences = sentences.Distinct(StringComparer.OrdinalIgnoreCase).Count();
        if (sentences.Length > 2 && uniqueSentences < sentences.Length * 0.7)
        {
            issues.Add("Response contains repetitive content");
            score -= 0.3;
        }

        // Check for incomplete sentences
        if (response.Trim().EndsWith(',') || response.Trim().EndsWith("and", StringComparison.Ordinal))
        {
            issues.Add("Response appears incomplete");
            score -= 0.2;
        }

        score = Math.Max(0, score);
        return new CheckResult(score >= 0.7, score, issues);
    }

    private static CheckResult CheckSafety(string response)
    {
        var issues = new List<string>();
        var responseLower = response.ToLowerInvariant();

        foreach (var pattern in UnsafePatterns)
        {
            if (responseLower.Contains(pattern, StringComparison.Ordinal))
            {
                issues.Add($"Response may contain sensitive content related to: {pattern}");
            }
        }

        var score = issues.Count == 0 ? 1.0 : Math.Max(0.3, 1.0 - (issues.Count * 0.2));
        return new CheckResult(issues.Count == 0, score, issues);
    }

    private static async Task<CheckResult> CheckFactualityAsync(
        string response,
        List<string> context,
        CancellationToken cancellationToken)
    {
        await Task.Delay(10, cancellationToken).ConfigureAwait(false);

        var issues = new List<string>();

        // Check if response claims are grounded in context
        if (context.Count == 0)
        {
            // No context to verify against
            return new CheckResult(true, 0.7, ["No context available for factuality verification"]);
        }

        var contextText = string.Join(" ", context).ToLowerInvariant();
        var responseSentences = response.Split(['.', '!', '?'], StringSplitOptions.RemoveEmptyEntries);

        int groundedCount = 0;
        foreach (var sentence in responseSentences)
        {
            var words = sentence.ToLowerInvariant()
                .Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Where(w => w.Length > 4)
                .ToList();

            if (words.Count > 0)
            {
                var matchCount = words.Count(w => contextText.Contains(w, StringComparison.Ordinal));
                if (matchCount >= words.Count * 0.3)
                {
                    groundedCount++;
                }
            }
        }

        var groundingRatio = responseSentences.Length > 0
            ? (double)groundedCount / responseSentences.Length
            : 0.5;

        if (groundingRatio < 0.5)
        {
            issues.Add("Some claims in the response may not be grounded in the provided context");
        }

        var score = Math.Max(0.5, groundingRatio);
        return new CheckResult(groundingRatio >= 0.5, score, issues);
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

    private static StandardValidationConfig ParseCustomConfig(PluginConfig config)
    {
        if (config.CustomConfig == null)
        {
            return new StandardValidationConfig();
        }

        var root = config.CustomConfig.RootElement;
        var checks = new List<string>();

        if (root.TryGetProperty("checks", out var checksElement) && checksElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var check in checksElement.EnumerateArray())
            {
                var c = check.GetString();
                if (!string.IsNullOrEmpty(c))
                {
                    checks.Add(c);
                }
            }
        }

        return new StandardValidationConfig
        {
            Checks = checks.Count > 0 ? checks : DefaultChecks.ToList(),
            StrictMode = root.TryGetProperty("strictMode", out var sm) && sm.GetBoolean(),
            MinScore = root.TryGetProperty("minScore", out var ms) ? ms.GetDouble() : 0.7
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
                "response": { "type": "string", "description": "Response to validate" },
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
                "overallScore": { "type": "number" },
                "checkResults": {
                    "type": "object",
                    "additionalProperties": {
                        "type": "object",
                        "properties": {
                            "passed": { "type": "boolean" },
                            "score": { "type": "number" },
                            "issues": { "type": "array", "items": { "type": "string" } }
                        }
                    }
                },
                "suggestions": { "type": "array", "items": { "type": "string" } }
            },
            "required": ["passed", "overallScore", "checkResults", "suggestions"]
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
                "checks": {
                    "type": "array",
                    "items": { "type": "string", "enum": ["relevance", "coherence", "safety", "factuality"] },
                    "default": ["relevance", "coherence", "safety", "factuality"]
                },
                "strictMode": { "type": "boolean", "default": false },
                "minScore": { "type": "number", "minimum": 0, "maximum": 1, "default": 0.7 }
            }
        }
        """);
    }

    private sealed record CheckResult(bool Passed, double Score, List<string> Issues);

    private sealed class StandardValidationConfig
    {
        public IReadOnlyList<string> Checks { get; init; } = DefaultChecks.ToList();
        public bool StrictMode { get; init; }
        public double MinScore { get; init; } = 0.7;
    }
}
