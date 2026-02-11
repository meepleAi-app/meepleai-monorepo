// =============================================================================
// MeepleAI - RAG Plugin System
// Issue #3430 - Plugin Testing Framework
// =============================================================================

using System.Text.Json;
using Api.BoundedContexts.KnowledgeBase.Domain.Plugins.Contracts;
using Api.BoundedContexts.KnowledgeBase.Domain.Plugins.Enums;
using Api.BoundedContexts.KnowledgeBase.Domain.Plugins.Models;
using FluentAssertions;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Domain.Plugins.Testing;

/// <summary>
/// Static class containing contract verification methods for IRagPlugin implementations.
/// Use these methods to validate that a plugin properly implements the contract.
/// </summary>
public static class PluginContractTests
{
    /// <summary>
    /// Verifies that a plugin meets all contract requirements.
    /// </summary>
    /// <param name="plugin">The plugin to verify.</param>
    /// <returns>A contract verification result.</returns>
    public static ContractVerificationResult VerifyContract(IRagPlugin plugin)
    {
        var result = new ContractVerificationResult();

        // Identity verification
        result.AddResult("Id", VerifyId(plugin));
        result.AddResult("Name", VerifyName(plugin));
        result.AddResult("Version", VerifyVersion(plugin));
        result.AddResult("Category", VerifyCategory(plugin));

        // Schema verification
        result.AddResult("InputSchema", VerifyInputSchema(plugin));
        result.AddResult("OutputSchema", VerifyOutputSchema(plugin));
        result.AddResult("ConfigSchema", VerifyConfigSchema(plugin));

        // Metadata verification
        result.AddResult("Metadata", VerifyMetadata(plugin));

        return result;
    }

    /// <summary>
    /// Verifies plugin identity properties.
    /// </summary>
    public static VerificationResult VerifyId(IRagPlugin plugin)
    {
        var checks = new List<(bool passed, string message)>();

        // Not null or empty
        checks.Add((
            !string.IsNullOrWhiteSpace(plugin.Id),
            "Id must not be null or empty"));

        // Format check (lowercase alphanumeric with hyphens)
        if (!string.IsNullOrEmpty(plugin.Id))
        {
            var isValidFormat = System.Text.RegularExpressions.Regex.IsMatch(plugin.Id, @"^[a-z0-9-]+$", System.Text.RegularExpressions.RegexOptions.None, TimeSpan.FromSeconds(5));
            checks.Add((isValidFormat, "Id should be lowercase alphanumeric with hyphens only"));
        }

        // Length check
        if (!string.IsNullOrEmpty(plugin.Id))
        {
            checks.Add((plugin.Id.Length <= 100, "Id should not exceed 100 characters"));
            checks.Add((plugin.Id.Length >= 3, "Id should be at least 3 characters"));
        }

        return CreateResult(checks);
    }

    /// <summary>
    /// Verifies plugin name.
    /// </summary>
    public static VerificationResult VerifyName(IRagPlugin plugin)
    {
        var checks = new List<(bool passed, string message)>();

        checks.Add((
            !string.IsNullOrWhiteSpace(plugin.Name),
            "Name must not be null or empty"));

        if (!string.IsNullOrEmpty(plugin.Name))
        {
            checks.Add((plugin.Name.Length <= 200, "Name should not exceed 200 characters"));
        }

        return CreateResult(checks);
    }

    /// <summary>
    /// Verifies plugin version follows semantic versioning.
    /// </summary>
    public static VerificationResult VerifyVersion(IRagPlugin plugin)
    {
        var checks = new List<(bool passed, string message)>();

        checks.Add((
            !string.IsNullOrWhiteSpace(plugin.Version),
            "Version must not be null or empty"));

        if (!string.IsNullOrEmpty(plugin.Version))
        {
            var semverPattern = @"^\d+\.\d+\.\d+(-[a-zA-Z0-9]+)?$";
            var isValidSemver = System.Text.RegularExpressions.Regex.IsMatch(plugin.Version, semverPattern, System.Text.RegularExpressions.RegexOptions.None, TimeSpan.FromSeconds(5));
            checks.Add((isValidSemver, "Version should follow semantic versioning (e.g., 1.0.0)"));
        }

        return CreateResult(checks);
    }

    /// <summary>
    /// Verifies plugin category is valid.
    /// </summary>
    public static VerificationResult VerifyCategory(IRagPlugin plugin)
    {
        var checks = new List<(bool passed, string message)>();

        checks.Add((
            Enum.IsDefined(typeof(PluginCategory), plugin.Category),
            "Category must be a valid PluginCategory enum value"));

        return CreateResult(checks);
    }

    /// <summary>
    /// Verifies input schema is valid JSON Schema.
    /// </summary>
    public static VerificationResult VerifyInputSchema(IRagPlugin plugin)
    {
        return VerifyJsonSchema(plugin.InputSchema, "InputSchema");
    }

    /// <summary>
    /// Verifies output schema is valid JSON Schema.
    /// </summary>
    public static VerificationResult VerifyOutputSchema(IRagPlugin plugin)
    {
        return VerifyJsonSchema(plugin.OutputSchema, "OutputSchema");
    }

    /// <summary>
    /// Verifies config schema is valid JSON Schema.
    /// </summary>
    public static VerificationResult VerifyConfigSchema(IRagPlugin plugin)
    {
        return VerifyJsonSchema(plugin.ConfigSchema, "ConfigSchema");
    }

    /// <summary>
    /// Verifies metadata is consistent with plugin properties.
    /// </summary>
    public static VerificationResult VerifyMetadata(IRagPlugin plugin)
    {
        var checks = new List<(bool passed, string message)>();
        var metadata = plugin.Metadata;

        checks.Add((metadata != null, "Metadata must not be null"));

        if (metadata != null)
        {
            checks.Add((metadata.Id == plugin.Id, "Metadata.Id must match Plugin.Id"));
            checks.Add((metadata.Name == plugin.Name, "Metadata.Name must match Plugin.Name"));
            checks.Add((metadata.Version == plugin.Version, "Metadata.Version must match Plugin.Version"));
            checks.Add((metadata.Category == plugin.Category, "Metadata.Category must match Plugin.Category"));
        }

        return CreateResult(checks);
    }

    /// <summary>
    /// Verifies that ValidateConfig handles all edge cases correctly.
    /// </summary>
    public static async Task<VerificationResult> VerifyValidationBehaviorAsync(IRagPlugin plugin)
    {
        var checks = new List<(bool passed, string message)>();

        // Default config should be valid
        var defaultConfig = PluginConfig.Default();
        var defaultResult = plugin.ValidateConfig(defaultConfig);
        checks.Add((defaultResult.IsValid, "Default configuration should be valid"));

        // Negative timeout should be invalid
        var negativeTimeout = new PluginConfig { TimeoutMs = -1 };
        var negativeTimeoutResult = plugin.ValidateConfig(negativeTimeout);
        checks.Add((!negativeTimeoutResult.IsValid, "Negative timeout should be invalid"));

        // Zero timeout should be invalid
        var zeroTimeout = new PluginConfig { TimeoutMs = 0 };
        var zeroTimeoutResult = plugin.ValidateConfig(zeroTimeout);
        checks.Add((!zeroTimeoutResult.IsValid, "Zero timeout should be invalid"));

        // Negative retries should be invalid
        var negativeRetries = new PluginConfig { TimeoutMs = 30000, MaxRetries = -1 };
        var negativeRetriesResult = plugin.ValidateConfig(negativeRetries);
        checks.Add((!negativeRetriesResult.IsValid, "Negative retries should be invalid"));

        // Valid input should pass
        var validInput = PluginMocks.CreateValidInput();
        var validInputResult = plugin.ValidateInput(validInput);
        checks.Add((validInputResult.IsValid, "Valid input should pass validation"));

        // Empty execution ID should fail
        var emptyIdInput = PluginMocks.CreateInvalidInput_EmptyExecutionId();
        var emptyIdResult = plugin.ValidateInput(emptyIdInput);
        checks.Add((!emptyIdResult.IsValid, "Empty execution ID should fail validation"));

        return await Task.FromResult(CreateResult(checks));
    }

    /// <summary>
    /// Verifies that ExecuteAsync behaves correctly under various conditions.
    /// </summary>
    public static async Task<VerificationResult> VerifyExecutionBehaviorAsync(
        IRagPlugin plugin,
        PluginInput input,
        PluginConfig? config = null)
    {
        var checks = new List<(bool passed, string message)>();
        config ??= PluginConfig.Default();

        try
        {
            var result = await plugin.ExecuteAsync(input, config);

            checks.Add((result != null, "ExecuteAsync must return a non-null output"));
            checks.Add((result!.ExecutionId == input.ExecutionId, "Output.ExecutionId must match input"));
            checks.Add((result.Metrics != null, "Output.Metrics must not be null"));
            checks.Add((result.Metrics!.DurationMs >= 0, "Execution duration must be non-negative"));

            if (result.Success)
            {
                // Successful execution should have a result or at least no error
                checks.Add((
                    result.ErrorMessage == null || result.Result != null,
                    "Successful execution should have result or no error"));
            }
            else
            {
                // Failed execution should have error information
                checks.Add((
                    !string.IsNullOrEmpty(result.ErrorMessage),
                    "Failed execution should have an error message"));
            }
        }
        catch (OperationCanceledException)
        {
            // This is acceptable if the task was cancelled
            checks.Add((true, "OperationCanceledException is acceptable"));
        }
        catch (Exception ex)
        {
            checks.Add((false, $"Unexpected exception: {ex.GetType().Name}: {ex.Message}"));
        }

        return CreateResult(checks);
    }

    /// <summary>
    /// Verifies health check behavior.
    /// </summary>
    public static async Task<VerificationResult> VerifyHealthCheckBehaviorAsync(IRagPlugin plugin)
    {
        var checks = new List<(bool passed, string message)>();

        try
        {
            var result = await plugin.HealthCheckAsync();

            checks.Add((Enum.IsDefined(typeof(HealthStatus), result.Status), "Health status must be valid"));
            checks.Add((result.CheckDurationMs >= 0, "Check duration must be non-negative"));
        }
        catch (OperationCanceledException)
        {
            checks.Add((true, "OperationCanceledException is acceptable for health check"));
        }
        catch (Exception ex)
        {
            checks.Add((false, $"Health check threw unexpected exception: {ex.GetType().Name}: {ex.Message}"));
        }

        return CreateResult(checks);
    }

    #region Private Helpers

    private static VerificationResult VerifyJsonSchema(JsonDocument? schema, string schemaName)
    {
        var checks = new List<(bool passed, string message)>();

        checks.Add((schema != null, $"{schemaName} must not be null"));

        if (schema != null)
        {
            var root = schema.RootElement;

            // Check for $schema property (recommended)
            if (root.TryGetProperty("$schema", out var schemaUri))
            {
                var uri = schemaUri.GetString();
                checks.Add((
                    uri?.Contains("json-schema.org") == true,
                    $"{schemaName} $schema should reference json-schema.org"));
            }

            // Check for type property
            if (root.TryGetProperty("type", out var typeElement))
            {
                var validTypes = new[] { "object", "array", "string", "number", "integer", "boolean", "null" };
                var typeValue = typeElement.GetString();
                checks.Add((
                    validTypes.Contains(typeValue),
                    $"{schemaName} type should be a valid JSON Schema type"));
            }

            // Verify it's parseable JSON
            try
            {
                var json = schema.RootElement.GetRawText();
                using var _ = JsonDocument.Parse(json);
                checks.Add((true, $"{schemaName} is valid JSON"));
            }
            catch (JsonException)
            {
                checks.Add((false, $"{schemaName} is not valid JSON"));
            }
        }

        return CreateResult(checks);
    }

    private static VerificationResult CreateResult(List<(bool passed, string message)> checks)
    {
        var passed = checks.Where(c => c.passed).Select(c => c.message).ToList();
        var failed = checks.Where(c => !c.passed).Select(c => c.message).ToList();

        return new VerificationResult
        {
            Passed = failed.Count == 0,
            PassedChecks = passed,
            FailedChecks = failed
        };
    }

    #endregion
}

/// <summary>
/// Result of a single verification check.
/// </summary>
public sealed record VerificationResult
{
    /// <summary>
    /// Whether all checks passed.
    /// </summary>
    public bool Passed { get; init; }

    /// <summary>
    /// List of passed check messages.
    /// </summary>
    public IReadOnlyList<string> PassedChecks { get; init; } = [];

    /// <summary>
    /// List of failed check messages.
    /// </summary>
    public IReadOnlyList<string> FailedChecks { get; init; } = [];

    /// <summary>
    /// Total number of checks performed.
    /// </summary>
    public int TotalChecks => PassedChecks.Count + FailedChecks.Count;
}

/// <summary>
/// Result of complete contract verification.
/// </summary>
public sealed class ContractVerificationResult
{
    private readonly Dictionary<string, VerificationResult> _results = new(StringComparer.Ordinal);

    /// <summary>
    /// Whether all contract requirements are met.
    /// </summary>
    public bool AllPassed => _results.Values.All(r => r.Passed);

    /// <summary>
    /// Individual verification results by category.
    /// </summary>
    public IReadOnlyDictionary<string, VerificationResult> Results => _results;

    /// <summary>
    /// Total number of passed checks.
    /// </summary>
    public int TotalPassed => _results.Values.Sum(r => r.PassedChecks.Count);

    /// <summary>
    /// Total number of failed checks.
    /// </summary>
    public int TotalFailed => _results.Values.Sum(r => r.FailedChecks.Count);

    /// <summary>
    /// All failed check messages.
    /// </summary>
    public IEnumerable<string> AllFailedChecks =>
        _results.SelectMany(kv => kv.Value.FailedChecks.Select(f => $"{kv.Key}: {f}"));

    internal void AddResult(string category, VerificationResult result)
    {
        _results[category] = result;
    }

    /// <summary>
    /// Asserts that all contract checks passed using FluentAssertions.
    /// </summary>
    public void AssertAllPassed()
    {
        if (!AllPassed)
        {
            var failures = string.Join(Environment.NewLine, AllFailedChecks);
            AllPassed.Should().BeTrue($"Contract verification failed:{Environment.NewLine}{failures}");
        }
    }

    /// <summary>
    /// Returns a formatted summary of the verification.
    /// </summary>
    public override string ToString()
    {
        var status = AllPassed ? "PASSED" : "FAILED";
        var summary = $"""
            Contract Verification: {status}
            Passed: {TotalPassed} / {TotalPassed + TotalFailed}
            """;

        if (!AllPassed)
        {
            summary += $"""


                Failed Checks:
                {string.Join(Environment.NewLine, AllFailedChecks.Select(f => $"  - {f}"))}
                """;
        }

        return summary;
    }
}
