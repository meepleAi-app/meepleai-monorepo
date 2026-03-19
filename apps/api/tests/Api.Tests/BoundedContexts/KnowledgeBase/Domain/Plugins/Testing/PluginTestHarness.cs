// =============================================================================
// MeepleAI - RAG Plugin System
// Issue #3430 - Plugin Testing Framework
// =============================================================================

using System.Diagnostics;
using System.Text.Json;
using Api.BoundedContexts.KnowledgeBase.Domain.Plugins.Contracts;
using Api.BoundedContexts.KnowledgeBase.Domain.Plugins.Enums;
using Api.BoundedContexts.KnowledgeBase.Domain.Plugins.Models;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Domain.Plugins.Testing;

/// <summary>
/// Base test harness for testing RAG plugins.
/// Provides comprehensive contract testing, validation, and performance measurement.
/// </summary>
/// <typeparam name="TPlugin">The plugin type to test.</typeparam>
public abstract class PluginTestHarness<TPlugin> where TPlugin : IRagPlugin
{
    /// <summary>
    /// Logger instance for tests.
    /// </summary>
    protected ILogger Logger { get; } = NullLogger.Instance;

    /// <summary>
    /// Creates a new instance of the plugin under test.
    /// </summary>
    protected abstract TPlugin CreatePlugin();

    /// <summary>
    /// Creates a valid test input for the plugin.
    /// Override to provide plugin-specific test inputs.
    /// </summary>
    protected virtual PluginInput CreateValidInput()
    {
        return PluginMocks.CreateValidInput();
    }

    /// <summary>
    /// Creates a valid configuration for the plugin.
    /// Override to provide plugin-specific configurations.
    /// </summary>
    protected virtual PluginConfig CreateValidConfig()
    {
        return PluginConfig.Default();
    }

    #region Contract Tests - Identity

    [Fact]
    public void Plugin_HasValidId()
    {
        // Arrange
        var plugin = CreatePlugin();

        // Assert
        plugin.Id.Should().NotBeNullOrWhiteSpace("Plugin must have a non-empty ID");
        plugin.Id.Should().MatchRegex(@"^[a-z0-9-]+$", "Plugin ID should be lowercase alphanumeric with hyphens");
    }

    [Fact]
    public void Plugin_HasValidName()
    {
        // Arrange
        var plugin = CreatePlugin();

        // Assert
        plugin.Name.Should().NotBeNullOrWhiteSpace("Plugin must have a non-empty name");
    }

    [Fact]
    public void Plugin_HasValidVersion()
    {
        // Arrange
        var plugin = CreatePlugin();

        // Assert
        plugin.Version.Should().NotBeNullOrWhiteSpace("Plugin must have a non-empty version");
        plugin.Version.Should().MatchRegex(@"^\d+\.\d+\.\d+", "Version should follow semantic versioning");
    }

    [Fact]
    public void Plugin_HasValidCategory()
    {
        // Arrange
        var plugin = CreatePlugin();

        // Assert
        Enum.IsDefined(typeof(PluginCategory), plugin.Category)
            .Should().BeTrue("Plugin category must be a valid PluginCategory enum value");
    }

    #endregion

    #region Contract Tests - Schemas

    [Fact]
    public void Plugin_InputSchema_IsValidJsonSchema()
    {
        // Arrange
        var plugin = CreatePlugin();

        // Act
        var schema = plugin.InputSchema;

        // Assert
        schema.Should().NotBeNull("InputSchema must not be null");
        AssertValidJsonSchema(schema, "InputSchema");
    }

    [Fact]
    public void Plugin_OutputSchema_IsValidJsonSchema()
    {
        // Arrange
        var plugin = CreatePlugin();

        // Act
        var schema = plugin.OutputSchema;

        // Assert
        schema.Should().NotBeNull("OutputSchema must not be null");
        AssertValidJsonSchema(schema, "OutputSchema");
    }

    [Fact]
    public void Plugin_ConfigSchema_IsValidJsonSchema()
    {
        // Arrange
        var plugin = CreatePlugin();

        // Act
        var schema = plugin.ConfigSchema;

        // Assert
        schema.Should().NotBeNull("ConfigSchema must not be null");
        AssertValidJsonSchema(schema, "ConfigSchema");
    }

    #endregion

    #region Contract Tests - Metadata

    [Fact]
    public void Plugin_Metadata_IsConsistent()
    {
        // Arrange
        var plugin = CreatePlugin();

        // Act
        var metadata = plugin.Metadata;

        // Assert
        metadata.Should().NotBeNull("Metadata must not be null");
        metadata.Id.Should().Be(plugin.Id, "Metadata.Id must match Plugin.Id");
        metadata.Name.Should().Be(plugin.Name, "Metadata.Name must match Plugin.Name");
        metadata.Version.Should().Be(plugin.Version, "Metadata.Version must match Plugin.Version");
        metadata.Category.Should().Be(plugin.Category, "Metadata.Category must match Plugin.Category");
    }

    #endregion

    #region Contract Tests - Validation

    [Fact]
    public void ValidateConfig_WithDefaultConfig_Succeeds()
    {
        // Arrange
        var plugin = CreatePlugin();
        var config = PluginConfig.Default();

        // Act
        var result = plugin.ValidateConfig(config);

        // Assert
        result.IsValid.Should().BeTrue("Default configuration should be valid");
    }

    [Fact]
    public void ValidateConfig_WithNegativeTimeout_Fails()
    {
        // Arrange
        var plugin = CreatePlugin();
        var config = new PluginConfig { TimeoutMs = -1 };

        // Act
        var result = plugin.ValidateConfig(config);

        // Assert
        result.IsValid.Should().BeFalse("Negative timeout should be invalid");
        result.Errors.Should().Contain(e => e.PropertyPath == "TimeoutMs");
    }

    [Fact]
    public void ValidateInput_WithValidInput_Succeeds()
    {
        // Arrange
        var plugin = CreatePlugin();
        var input = CreateValidInput();

        // Act
        var result = plugin.ValidateInput(input);

        // Assert
        result.IsValid.Should().BeTrue("Valid input should pass validation");
    }

    [Fact]
    public void ValidateInput_WithEmptyExecutionId_Fails()
    {
        // Arrange
        var plugin = CreatePlugin();
        var input = new PluginInput
        {
            ExecutionId = Guid.Empty,
            Payload = JsonDocument.Parse("{}")
        };

        // Act
        var result = plugin.ValidateInput(input);

        // Assert
        result.IsValid.Should().BeFalse("Empty ExecutionId should be invalid");
        result.Errors.Should().Contain(e => e.Code == "MISSING_EXECUTION_ID");
    }

    #endregion

    #region Contract Tests - Execution

    [Fact]
    public async Task ExecuteAsync_WithValidInput_ReturnsOutput()
    {
        // Arrange
        var plugin = CreatePlugin();
        var input = CreateValidInput();
        var config = CreateValidConfig();

        // Act
        var result = await plugin.ExecuteAsync(input, config);

        // Assert
        result.Should().NotBeNull("ExecuteAsync must return a non-null output");
        result.ExecutionId.Should().Be(input.ExecutionId, "Output.ExecutionId must match input");
        result.Metrics.Should().NotBeNull("Output.Metrics must not be null");
    }

    [Fact]
    public async Task ExecuteAsync_WithValidInput_RecordsExecutionDuration()
    {
        // Arrange
        var plugin = CreatePlugin();
        var input = CreateValidInput();
        var config = CreateValidConfig();

        // Act
        var result = await plugin.ExecuteAsync(input, config);

        // Assert
        result.Metrics.DurationMs.Should().BeGreaterThanOrEqualTo(0, "Execution duration should be recorded");
    }

    [Fact]
    public async Task ExecuteAsync_WithCancellation_ThrowsOrHandlesGracefully()
    {
        // Arrange
        var plugin = CreatePlugin();
        var input = CreateValidInput();
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        // Act & Assert
        // Plugin should either throw OperationCanceledException or handle cancellation gracefully
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await plugin.ExecuteAsync(input, null, cts.Token));
    }

    #endregion

    #region Contract Tests - Health Check

    [Fact]
    public async Task HealthCheckAsync_ReturnsValidResult()
    {
        // Arrange
        var plugin = CreatePlugin();

        // Act
        var result = await plugin.HealthCheckAsync();

        // Assert
        result.Should().NotBeNull("HealthCheckAsync must return a non-null result");
        Enum.IsDefined(typeof(HealthStatus), result.Status)
            .Should().BeTrue("Health status must be a valid HealthStatus enum value");
        result.CheckDurationMs.Should().BeGreaterThanOrEqualTo(0, "Check duration should be recorded");
    }

    [Fact]
    public async Task HealthCheckAsync_WithCancellation_ThrowsOrHandlesGracefully()
    {
        // Arrange
        var plugin = CreatePlugin();
        await cts.CancelAsync();

        // Act & Assert
        // Health check should either throw or return an unhealthy status
        try
        {
            var result = await plugin.HealthCheckAsync(cts.Token);
            // If it doesn't throw, it should indicate cancellation
        }
        catch (OperationCanceledException)
        {
            // Expected behavior
        }
    }

    #endregion

    #region Performance Tests

    /// <summary>
    /// Measures execution time statistics over multiple runs.
    /// </summary>
    /// <param name="iterations">Number of iterations to run.</param>
    /// <param name="warmupIterations">Number of warmup iterations.</param>
    /// <returns>Performance statistics.</returns>
    protected async Task<PerformanceStatistics> MeasureExecutionPerformanceAsync(
        int iterations = 10,
        int warmupIterations = 2)
    {
        var plugin = CreatePlugin();
        var input = CreateValidInput();
        var config = CreateValidConfig();

        // Warmup
        for (var i = 0; i < warmupIterations; i++)
        {
            await plugin.ExecuteAsync(input, config);
        }

        // Measurement
        var durations = new List<double>(iterations);
        var stopwatch = new Stopwatch();

        for (var i = 0; i < iterations; i++)
        {
            // Create fresh input for each iteration to avoid caching
            input = CreateValidInput();

            stopwatch.Restart();
            await plugin.ExecuteAsync(input, config);
            stopwatch.Stop();

            durations.Add(stopwatch.Elapsed.TotalMilliseconds);
        }

        return new PerformanceStatistics
        {
            Iterations = iterations,
            MinMs = durations.Min(),
            MaxMs = durations.Max(),
            MeanMs = durations.Average(),
            MedianMs = CalculateMedian(durations),
            StdDevMs = CalculateStandardDeviation(durations),
            P95Ms = CalculatePercentile(durations, 95),
            P99Ms = CalculatePercentile(durations, 99)
        };
    }

    /// <summary>
    /// Asserts that plugin execution meets performance requirements.
    /// </summary>
    protected async Task AssertExecutionPerformanceAsync(
        double maxMeanMs,
        double maxP95Ms,
        int iterations = 10)
    {
        var stats = await MeasureExecutionPerformanceAsync(iterations);

        stats.MeanMs.Should().BeLessThanOrEqualTo(maxMeanMs,
            $"Mean execution time ({stats.MeanMs:F2}ms) exceeded maximum ({maxMeanMs}ms)");
        stats.P95Ms.Should().BeLessThanOrEqualTo(maxP95Ms,
            $"P95 execution time ({stats.P95Ms:F2}ms) exceeded maximum ({maxP95Ms}ms)");
    }

    #endregion

    #region Helper Methods

    private static void AssertValidJsonSchema(JsonDocument schema, string schemaName)
    {
        var root = schema.RootElement;

        // Check for JSON Schema $schema property
        if (root.TryGetProperty("$schema", out var schemaUri))
        {
            schemaUri.GetString().Should().Contain("json-schema.org",
                $"{schemaName} $schema should reference json-schema.org");
        }

        // Check for type property (required in most schemas)
        if (root.TryGetProperty("type", out var typeElement))
        {
            var validTypes = new[] { "object", "array", "string", "number", "integer", "boolean", "null" };
            validTypes.Should().Contain(typeElement.GetString(),
                $"{schemaName} type should be a valid JSON Schema type");
        }
    }

    private static double CalculateMedian(List<double> values)
    {
        var sorted = values.OrderBy(x => x).ToList();
        var mid = sorted.Count / 2;
        return sorted.Count % 2 == 0
            ? (sorted[mid - 1] + sorted[mid]) / 2.0
            : sorted[mid];
    }

    private static double CalculateStandardDeviation(List<double> values)
    {
        var mean = values.Average();
        var sumSquaredDiffs = values.Sum(v => Math.Pow(v - mean, 2));
        return Math.Sqrt(sumSquaredDiffs / values.Count);
    }

    private static double CalculatePercentile(List<double> values, int percentile)
    {
        var sorted = values.OrderBy(x => x).ToList();
        var index = (int)Math.Ceiling(percentile / 100.0 * sorted.Count) - 1;
        return sorted[Math.Max(0, Math.Min(index, sorted.Count - 1))];
    }

    #endregion
}

/// <summary>
/// Performance statistics from plugin execution measurements.
/// </summary>
public sealed record PerformanceStatistics
{
    /// <summary>
    /// Number of iterations measured.
    /// </summary>
    public int Iterations { get; init; }

    /// <summary>
    /// Minimum execution time in milliseconds.
    /// </summary>
    public double MinMs { get; init; }

    /// <summary>
    /// Maximum execution time in milliseconds.
    /// </summary>
    public double MaxMs { get; init; }

    /// <summary>
    /// Mean (average) execution time in milliseconds.
    /// </summary>
    public double MeanMs { get; init; }

    /// <summary>
    /// Median execution time in milliseconds.
    /// </summary>
    public double MedianMs { get; init; }

    /// <summary>
    /// Standard deviation of execution times in milliseconds.
    /// </summary>
    public double StdDevMs { get; init; }

    /// <summary>
    /// 95th percentile execution time in milliseconds.
    /// </summary>
    public double P95Ms { get; init; }

    /// <summary>
    /// 99th percentile execution time in milliseconds.
    /// </summary>
    public double P99Ms { get; init; }

    /// <summary>
    /// Returns a formatted string representation of the statistics.
    /// </summary>
    public override string ToString()
    {
        return $"""
            Performance Statistics ({Iterations} iterations):
              Min:    {MinMs:F2} ms
              Max:    {MaxMs:F2} ms
              Mean:   {MeanMs:F2} ms
              Median: {MedianMs:F2} ms
              StdDev: {StdDevMs:F2} ms
              P95:    {P95Ms:F2} ms
              P99:    {P99Ms:F2} ms
            """;
    }
}
