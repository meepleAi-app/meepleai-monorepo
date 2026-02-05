// =============================================================================
// MeepleAI - RAG Plugin System
// Issue #3430 - Plugin Testing Framework Tests
// =============================================================================

using System.Text.Json;
using Api.BoundedContexts.KnowledgeBase.Domain.Plugins.Base;
using Api.BoundedContexts.KnowledgeBase.Domain.Plugins.Enums;
using Api.BoundedContexts.KnowledgeBase.Domain.Plugins.Models;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Domain.Plugins.Testing;

/// <summary>
/// Tests for the plugin testing framework itself.
/// Verifies that the test harness, mocks, and benchmarks work correctly.
/// </summary>
public class PluginTestFrameworkTests
{
    #region PluginMocks Tests

    [Fact]
    public void CreateValidInput_ReturnsInputWithNonEmptyExecutionId()
    {
        // Act
        var input = PluginMocks.CreateValidInput();

        // Assert
        input.Should().NotBeNull();
        input.ExecutionId.Should().NotBe(Guid.Empty);
        input.Payload.Should().NotBeNull();
    }

    [Fact]
    public void CreateValidInput_WithExecutionId_UsesProvidedId()
    {
        // Arrange
        var expectedId = Guid.NewGuid();

        // Act
        var input = PluginMocks.CreateValidInput(expectedId);

        // Assert
        input.ExecutionId.Should().Be(expectedId);
    }

    [Fact]
    public void CreateInputWithPayload_ParsesJsonCorrectly()
    {
        // Arrange
        var json = """{"key": "value", "number": 42}""";

        // Act
        var input = PluginMocks.CreateInputWithPayload(json);

        // Assert
        input.Payload.RootElement.GetProperty("key").GetString().Should().Be("value");
        input.Payload.RootElement.GetProperty("number").GetInt32().Should().Be(42);
    }

    [Fact]
    public void CreateQueryInput_IncludesQueryAndOptionalIds()
    {
        // Arrange
        var query = "How do I play this game?";
        var gameId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        // Act
        var input = PluginMocks.CreateQueryInput(query, gameId, userId);

        // Assert
        input.GameId.Should().Be(gameId);
        input.UserId.Should().Be(userId);
        input.Payload.RootElement.GetProperty("query").GetString().Should().Be(query);
    }

    [Fact]
    public void CreateSuccessfulOutput_SetsSuccessToTrue()
    {
        // Arrange
        var executionId = Guid.NewGuid();

        // Act
        var output = PluginMocks.CreateSuccessfulOutput(executionId);

        // Assert
        output.Success.Should().BeTrue();
        output.ExecutionId.Should().Be(executionId);
        output.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public void CreateFailedOutput_SetsSuccessToFalseWithError()
    {
        // Arrange
        var executionId = Guid.NewGuid();
        var errorMessage = "Test error";
        var errorCode = "TEST_ERROR";

        // Act
        var output = PluginMocks.CreateFailedOutput(executionId, errorMessage, errorCode);

        // Assert
        output.Success.Should().BeFalse();
        output.ErrorMessage.Should().Be(errorMessage);
        output.ErrorCode.Should().Be(errorCode);
    }

    [Fact]
    public void CreateRetrievedDocuments_CreatesCorrectCount()
    {
        // Act
        var docs = PluginMocks.CreateRetrievedDocuments(5);

        // Assert
        docs.Should().HaveCount(5);
        docs.Select(d => d.Id).Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void CreateInvalidInput_EmptyExecutionId_HasEmptyGuid()
    {
        // Act
        var input = PluginMocks.CreateInvalidInput_EmptyExecutionId();

        // Assert
        input.ExecutionId.Should().Be(Guid.Empty);
    }

    #endregion

    #region PluginContractTests Tests

    [Fact]
    public void VerifyContract_WithValidPlugin_AllChecksPassing()
    {
        // Arrange
        var plugin = new SampleTestPlugin(NullLogger.Instance);

        // Act
        var result = PluginContractTests.VerifyContract(plugin);

        // Assert
        result.AllPassed.Should().BeTrue(result.ToString());
    }

    [Fact]
    public void VerifyId_WithValidId_Passes()
    {
        // Arrange
        var plugin = new SampleTestPlugin(NullLogger.Instance);

        // Act
        var result = PluginContractTests.VerifyId(plugin);

        // Assert
        result.Passed.Should().BeTrue();
    }

    [Fact]
    public void VerifyVersion_WithSemVer_Passes()
    {
        // Arrange
        var plugin = new SampleTestPlugin(NullLogger.Instance);

        // Act
        var result = PluginContractTests.VerifyVersion(plugin);

        // Assert
        result.Passed.Should().BeTrue();
    }

    [Fact]
    public async Task VerifyValidationBehavior_WithValidPlugin_AllChecksPassing()
    {
        // Arrange
        var plugin = new SampleTestPlugin(NullLogger.Instance);

        // Act
        var result = await PluginContractTests.VerifyValidationBehaviorAsync(plugin);

        // Assert
        result.Passed.Should().BeTrue(string.Join(", ", result.FailedChecks));
    }

    [Fact]
    public async Task VerifyExecutionBehavior_WithValidPlugin_AllChecksPassing()
    {
        // Arrange
        var plugin = new SampleTestPlugin(NullLogger.Instance);
        var input = PluginMocks.CreateValidInput();

        // Act
        var result = await PluginContractTests.VerifyExecutionBehaviorAsync(plugin, input);

        // Assert
        result.Passed.Should().BeTrue(string.Join(", ", result.FailedChecks));
    }

    [Fact]
    public async Task VerifyHealthCheckBehavior_WithValidPlugin_AllChecksPassing()
    {
        // Arrange
        var plugin = new SampleTestPlugin(NullLogger.Instance);

        // Act
        var result = await PluginContractTests.VerifyHealthCheckBehaviorAsync(plugin);

        // Assert
        result.Passed.Should().BeTrue(string.Join(", ", result.FailedChecks));
    }

    #endregion

    #region PluginBenchmarks Tests

    [Fact]
    public async Task RunBenchmarkAsync_WithQuickOptions_CompletesSuccessfully()
    {
        // Arrange
        var plugin = new SampleTestPlugin(NullLogger.Instance);
        Func<PluginInput> inputGenerator = PluginMocks.CreateValidInput;

        // Act
        var result = await PluginBenchmarks.RunBenchmarkAsync(
            plugin,
            inputGenerator,
            options: BenchmarkOptions.Quick);

        // Assert
        result.Should().NotBeNull();
        result.Durations.Should().HaveCount(BenchmarkOptions.Quick.Iterations);
        result.SuccessRate.Should().Be(1.0);
        result.MeanMs.Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public async Task RunThroughputBenchmarkAsync_WithShortDuration_MeasuresThroughput()
    {
        // Arrange
        var plugin = new SampleTestPlugin(NullLogger.Instance);
        Func<PluginInput> inputGenerator = PluginMocks.CreateValidInput;

        // Act
        var result = await PluginBenchmarks.RunThroughputBenchmarkAsync(
            plugin,
            inputGenerator,
            durationSeconds: 1);

        // Assert
        result.Should().NotBeNull();
        result.TotalOperations.Should().BeGreaterThan(0);
        result.OperationsPerSecond.Should().BeGreaterThan(0);
        result.SuccessRate.Should().Be(1.0);
    }

    [Fact]
    public async Task RunConcurrencyBenchmarkAsync_WithMultipleTasks_CompletesSuccessfully()
    {
        // Arrange
        var plugin = new SampleTestPlugin(NullLogger.Instance);
        Func<PluginInput> inputGenerator = PluginMocks.CreateValidInput;

        // Act
        var result = await PluginBenchmarks.RunConcurrencyBenchmarkAsync(
            plugin,
            inputGenerator,
            concurrency: 3,
            operationsPerTask: 2);

        // Assert
        result.Should().NotBeNull();
        result.TotalOperations.Should().Be(6); // 3 tasks * 2 ops
        result.SuccessCount.Should().Be(6);
        result.ThroughputPerSecond.Should().BeGreaterThan(0);
    }

    #endregion

    #region PluginTestHarness Integration Tests

    [Fact]
    public async Task SamplePluginTestHarness_AllContractTestsPass()
    {
        // This test uses the actual harness pattern
        var harness = new SamplePluginTestHarness();

        // Run a subset of contract tests via reflection to verify harness works
        harness.Plugin_HasValidId();
        harness.Plugin_HasValidName();
        harness.Plugin_HasValidVersion();
        harness.Plugin_HasValidCategory();
        harness.Plugin_InputSchema_IsValidJsonSchema();
        harness.Plugin_OutputSchema_IsValidJsonSchema();
        harness.Plugin_ConfigSchema_IsValidJsonSchema();
        harness.Plugin_Metadata_IsConsistent();
        harness.ValidateConfig_WithDefaultConfig_Succeeds();
        harness.ValidateInput_WithValidInput_Succeeds();

        await harness.ExecuteAsync_WithValidInput_ReturnsOutput();
        await harness.HealthCheckAsync_ReturnsValidResult();
    }

    [Fact]
    public async Task SamplePluginTestHarness_PerformanceMeasurement_Works()
    {
        // Arrange
        var harness = new SamplePluginTestHarness();

        // Act
        var stats = await harness.MeasurePerformanceAsync();

        // Assert
        stats.MeanMs.Should().BeGreaterThanOrEqualTo(0);
        stats.Iterations.Should().Be(5);
    }

    #endregion

    #region Sample Test Plugin and Harness

    /// <summary>
    /// Sample plugin for testing the test framework.
    /// </summary>
    private sealed class SampleTestPlugin : RagPluginBase
    {
        public override string Id => "sample-test-plugin-v1";
        public override string Name => "Sample Test Plugin";
        public override string Version => "1.0.0";
        public override PluginCategory Category => PluginCategory.Transform;

        public SampleTestPlugin(ILogger logger) : base(logger) { }

        protected override Task<PluginOutput> ExecuteCoreAsync(
            PluginInput input,
            PluginConfig config,
            CancellationToken cancellationToken)
        {
            var result = JsonDocument.Parse("""{"transformed": true}""");
            return Task.FromResult(PluginOutput.Successful(input.ExecutionId, result, 0.95));
        }

        protected override JsonDocument CreateInputSchema() =>
            CreateBasicSchema("object", "Sample input schema");

        protected override JsonDocument CreateOutputSchema() =>
            CreateBasicSchema("object", "Sample output schema");

        protected override JsonDocument CreateConfigSchema() =>
            CreateBasicSchema("object", "Sample config schema");
    }

    /// <summary>
    /// Sample test harness demonstrating framework usage.
    /// </summary>
    private sealed class SamplePluginTestHarness : PluginTestHarness<SampleTestPlugin>
    {
        protected override SampleTestPlugin CreatePlugin()
        {
            return new SampleTestPlugin(Logger);
        }

        public async Task<PerformanceStatistics> MeasurePerformanceAsync()
        {
            return await MeasureExecutionPerformanceAsync(iterations: 5, warmupIterations: 1);
        }
    }

    #endregion
}
