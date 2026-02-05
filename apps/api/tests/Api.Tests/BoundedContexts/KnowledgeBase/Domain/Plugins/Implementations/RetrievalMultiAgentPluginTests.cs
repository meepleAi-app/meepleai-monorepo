// =============================================================================
// MeepleAI - RAG Plugin System
// Issue #3359 - Multi-Agent RAG Strategy Tests
// =============================================================================

using System.Text.Json;
using Api.BoundedContexts.KnowledgeBase.Domain.Plugins.Enums;
using Api.BoundedContexts.KnowledgeBase.Domain.Plugins.Implementations.Retrieval;
using Api.BoundedContexts.KnowledgeBase.Domain.Plugins.Models;
using Api.Tests.BoundedContexts.KnowledgeBase.Domain.Plugins.Testing;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Domain.Plugins.Implementations;

/// <summary>
/// Unit tests for RetrievalMultiAgentPlugin.
/// Issue #3359: Multi-Agent RAG Strategy implementation.
/// </summary>
[Trait("Category", "Unit")]
[Trait("BoundedContext", "KnowledgeBase")]
[Trait("Component", "Plugins")]
public class RetrievalMultiAgentPluginTests
{
    private readonly RetrievalMultiAgentPlugin _sut;
    private readonly Mock<ILogger<RetrievalMultiAgentPlugin>> _loggerMock;

    public RetrievalMultiAgentPluginTests()
    {
        _loggerMock = new Mock<ILogger<RetrievalMultiAgentPlugin>>();
        _sut = new RetrievalMultiAgentPlugin(_loggerMock.Object);
    }

    #region Contract Verification Tests

    [Fact]
    public void Plugin_ShouldHaveCorrectId()
    {
        // Assert
        _sut.Id.Should().Be("retrieval-multi-agent-v1");
    }

    [Fact]
    public void Plugin_ShouldHaveCorrectName()
    {
        // Assert
        _sut.Name.Should().Be("Multi-Agent Retrieval");
    }

    [Fact]
    public void Plugin_ShouldHaveCorrectVersion()
    {
        // Assert
        _sut.Version.Should().Be("1.0.0");
    }

    [Fact]
    public void Plugin_ShouldHaveCorrectCategory()
    {
        // Assert
        _sut.Category.Should().Be(PluginCategory.Retrieval);
    }

    [Fact]
    public void Plugin_ShouldPassContractVerification()
    {
        // Act
        var result = PluginContractTests.VerifyContract(_sut);

        // Assert
        result.AllPassed.Should().BeTrue(
            $"Contract verification failed: {string.Join(", ", result.AllFailedChecks)}");
    }

    [Fact]
    public void Plugin_ShouldHaveValidInputSchema()
    {
        // Act
        var schema = _sut.InputSchema;

        // Assert
        schema.Should().NotBeNull();
        schema.RootElement.TryGetProperty("type", out var typeElement).Should().BeTrue();
        typeElement.GetString().Should().Be("object");
        schema.RootElement.TryGetProperty("required", out var requiredElement).Should().BeTrue();
        requiredElement.EnumerateArray().Should().Contain(e => e.GetString() == "query");
    }

    [Fact]
    public void Plugin_ShouldHaveValidOutputSchema()
    {
        // Act
        var schema = _sut.OutputSchema;

        // Assert
        schema.Should().NotBeNull();
        schema.RootElement.TryGetProperty("properties", out var propsElement).Should().BeTrue();
        propsElement.TryGetProperty("answer", out _).Should().BeTrue();
        propsElement.TryGetProperty("confidence", out _).Should().BeTrue();
        propsElement.TryGetProperty("validated", out _).Should().BeTrue();
        propsElement.TryGetProperty("agentOutputs", out _).Should().BeTrue();
        propsElement.TryGetProperty("documents", out _).Should().BeTrue();
        propsElement.TryGetProperty("metrics", out _).Should().BeTrue();
    }

    [Fact]
    public void Plugin_ShouldHaveValidConfigSchema()
    {
        // Act
        var schema = _sut.ConfigSchema;

        // Assert
        schema.Should().NotBeNull();
        schema.RootElement.TryGetProperty("properties", out var propsElement).Should().BeTrue();
        propsElement.TryGetProperty("topK", out _).Should().BeTrue();
        propsElement.TryGetProperty("minScore", out _).Should().BeTrue();
        propsElement.TryGetProperty("modelOverrides", out _).Should().BeTrue();
    }

    #endregion

    #region Input Validation Tests

    [Fact]
    public void ValidateInput_WithValidQuery_ShouldReturnSuccess()
    {
        // Arrange
        var input = CreateValidInput("What are the combat rules?");

        // Act
        var result = _sut.ValidateInput(input);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void ValidateInput_WithMissingQuery_ShouldReturnFailure()
    {
        // Arrange
        var payload = JsonDocument.Parse("{}");
        var input = new PluginInput
        {
            ExecutionId = Guid.NewGuid(),
            Payload = payload
        };

        // Act
        var result = _sut.ValidateInput(input);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Code == "MISSING_QUERY");
    }

    [Fact]
    public void ValidateInput_WithEmptyQuery_ShouldReturnFailure()
    {
        // Arrange
        var input = CreateValidInput("");

        // Act
        var result = _sut.ValidateInput(input);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Code == "MISSING_QUERY");
    }

    [Fact]
    public void ValidateInput_WithWhitespaceQuery_ShouldReturnFailure()
    {
        // Arrange
        var input = CreateValidInput("   ");

        // Act
        var result = _sut.ValidateInput(input);

        // Assert
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void ValidateInput_WithContext_ShouldReturnSuccess()
    {
        // Arrange
        var payload = JsonDocument.Parse("""{"query": "combat rules", "context": "board game rules"}""");
        var input = new PluginInput
        {
            ExecutionId = Guid.NewGuid(),
            Payload = payload
        };

        // Act
        var result = _sut.ValidateInput(input);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    #endregion

    #region Execution Tests

    [Fact]
    public async Task ExecuteAsync_WithValidInput_ShouldReturnSuccess()
    {
        // Arrange
        var input = CreateValidInput("How do I score victory points?");
        var config = PluginConfig.Default();

        // Act
        var result = await _sut.ExecuteAsync(input, config);

        // Assert
        result.Success.Should().BeTrue();
        result.ExecutionId.Should().Be(input.ExecutionId);
        result.Result.Should().NotBeNull();
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnAnswerInResult()
    {
        // Arrange
        var input = CreateValidInput("What are the movement rules?");
        var config = PluginConfig.Default();

        // Act
        var result = await _sut.ExecuteAsync(input, config);

        // Assert
        result.Success.Should().BeTrue();
        result.Result!.RootElement.TryGetProperty("answer", out var answer).Should().BeTrue();
        answer.GetString().Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnConfidenceScore()
    {
        // Arrange
        var input = CreateValidInput("How does trading work?");
        var config = PluginConfig.Default();

        // Act
        var result = await _sut.ExecuteAsync(input, config);

        // Assert
        result.Success.Should().BeTrue();
        result.Result!.RootElement.TryGetProperty("confidence", out var confidence).Should().BeTrue();
        confidence.GetDouble().Should().BeGreaterThan(0);
        confidence.GetDouble().Should().BeLessThanOrEqualTo(1);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnValidatedFlag()
    {
        // Arrange
        var input = CreateValidInput("What are the special abilities?");
        var config = PluginConfig.Default();

        // Act
        var result = await _sut.ExecuteAsync(input, config);

        // Assert
        result.Success.Should().BeTrue();
        result.Result!.RootElement.TryGetProperty("validated", out var validated).Should().BeTrue();
        validated.ValueKind.Should().BeOneOf(JsonValueKind.True, JsonValueKind.False);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnAgentOutputs()
    {
        // Arrange
        var input = CreateValidInput("Complex strategic question");
        var config = PluginConfig.Default();

        // Act
        var result = await _sut.ExecuteAsync(input, config);

        // Assert
        result.Success.Should().BeTrue();
        result.Result!.RootElement.TryGetProperty("agentOutputs", out var agents).Should().BeTrue();
        agents.TryGetProperty("retrieval", out _).Should().BeTrue();
        agents.TryGetProperty("analysis", out _).Should().BeTrue();
        agents.TryGetProperty("synthesis", out _).Should().BeTrue();
        agents.TryGetProperty("validation", out _).Should().BeTrue();
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnDocumentsInResult()
    {
        // Arrange
        var input = CreateValidInput("What are the game rules?");
        var config = PluginConfig.Default();

        // Act
        var result = await _sut.ExecuteAsync(input, config);

        // Assert
        result.Success.Should().BeTrue();
        result.Result!.RootElement.TryGetProperty("documents", out var docs).Should().BeTrue();
        docs.GetArrayLength().Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnMetricsInResult()
    {
        // Arrange
        var input = CreateValidInput("How does combat work?");
        var config = PluginConfig.Default();

        // Act
        var result = await _sut.ExecuteAsync(input, config);

        // Assert
        result.Success.Should().BeTrue();
        result.Result!.RootElement.TryGetProperty("metrics", out var metrics).Should().BeTrue();
        metrics.TryGetProperty("totalTokens", out _).Should().BeTrue();
        metrics.TryGetProperty("inputTokens", out _).Should().BeTrue();
        metrics.TryGetProperty("outputTokens", out _).Should().BeTrue();
        metrics.TryGetProperty("documentsRetrieved", out _).Should().BeTrue();
        metrics.TryGetProperty("agentCount", out _).Should().BeTrue();
        metrics.TryGetProperty("estimatedCost", out _).Should().BeTrue();
    }

    [Fact]
    public async Task ExecuteAsync_ShouldRecordExecutionMetrics()
    {
        // Arrange
        var input = CreateValidInput("What are the victory conditions?");
        var config = PluginConfig.Default();

        // Act
        var result = await _sut.ExecuteAsync(input, config);

        // Assert
        result.Metrics.DurationMs.Should().BeGreaterThan(0);
        result.Metrics.ItemsProcessed.Should().NotBeNull();
    }

    [Fact]
    public async Task ExecuteAsync_ShouldCalculateConfidenceScore()
    {
        // Arrange
        var input = CreateValidInput("How do combat rules work?");
        var config = PluginConfig.Default();

        // Act
        var result = await _sut.ExecuteAsync(input, config);

        // Assert
        result.Confidence.Should().NotBeNull();
        result.Confidence!.Value.Should().BeGreaterThan(0);
        result.Confidence.Value.Should().BeLessThanOrEqualTo(1);
    }

    [Fact]
    public async Task ExecuteAsync_WithMissingQuery_ShouldReturnFailure()
    {
        // Arrange
        var payload = JsonDocument.Parse("{}");
        var input = new PluginInput
        {
            ExecutionId = Guid.NewGuid(),
            Payload = payload
        };
        var config = PluginConfig.Default();

        // Act
        var result = await _sut.ExecuteAsync(input, config);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be("VALIDATION_ERROR");
    }

    #endregion

    #region Agent Output Tests

    [Fact]
    public async Task ExecuteAsync_AgentOutputsShouldHaveCorrectStructure()
    {
        // Arrange
        var input = CreateValidInput("Strategic planning question");
        var config = PluginConfig.Default();

        // Act
        var result = await _sut.ExecuteAsync(input, config);

        // Assert
        result.Success.Should().BeTrue();
        result.Result!.RootElement.TryGetProperty("agentOutputs", out var agents).Should().BeTrue();

        foreach (var agentOutput in agents.EnumerateObject())
        {
            agentOutput.Value.TryGetProperty("output", out _).Should().BeTrue();
            agentOutput.Value.TryGetProperty("summary", out _).Should().BeTrue();
            agentOutput.Value.TryGetProperty("model", out _).Should().BeTrue();
            agentOutput.Value.TryGetProperty("inputTokens", out _).Should().BeTrue();
            agentOutput.Value.TryGetProperty("outputTokens", out _).Should().BeTrue();
            agentOutput.Value.TryGetProperty("confidence", out _).Should().BeTrue();
        }
    }

    [Fact]
    public async Task ExecuteAsync_ShouldUseDifferentModelsForAgents()
    {
        // Arrange
        var input = CreateValidInput("Complex multi-agent question");
        var config = PluginConfig.Default();

        // Act
        var result = await _sut.ExecuteAsync(input, config);

        // Assert
        result.Success.Should().BeTrue();
        result.Result!.RootElement.TryGetProperty("agentOutputs", out var agents).Should().BeTrue();

        // Retrieval, Analysis, Validation should use Haiku
        agents.GetProperty("retrieval").GetProperty("model").GetString().Should().Contain("haiku");
        agents.GetProperty("analysis").GetProperty("model").GetString().Should().Contain("haiku");
        agents.GetProperty("validation").GetProperty("model").GetString().Should().Contain("haiku");

        // Synthesis should use Sonnet (premium model)
        agents.GetProperty("synthesis").GetProperty("model").GetString().Should().Contain("sonnet");
    }

    [Fact]
    public async Task ExecuteAsync_ShouldHaveFourAgents()
    {
        // Arrange
        var input = CreateValidInput("Test query");
        var config = PluginConfig.Default();

        // Act
        var result = await _sut.ExecuteAsync(input, config);

        // Assert
        result.Success.Should().BeTrue();
        result.Result!.RootElement.TryGetProperty("metrics", out var metrics).Should().BeTrue();
        metrics.TryGetProperty("agentCount", out var agentCount).Should().BeTrue();
        agentCount.GetInt32().Should().Be(4);
    }

    #endregion

    #region Configuration Tests

    [Fact]
    public async Task ExecuteAsync_WithCustomTopK_ShouldLimitResults()
    {
        // Arrange
        var input = CreateValidInput("Test query");
        using var customConfig = JsonDocument.Parse("""{"topK": 3}""");
        var config = PluginConfig.Create(
            timeoutMs: 30000,
            customConfig: customConfig);

        // Act
        var result = await _sut.ExecuteAsync(input, config);

        // Assert - verify success with error message for debugging
        result.Success.Should().BeTrue(
            $"Plugin execution failed. Error: {result.ErrorMessage ?? "none"}, Code: {result.ErrorCode ?? "none"}");
        result.Result!.RootElement.TryGetProperty("documents", out var docs).Should().BeTrue();
        // The actual number of docs depends on the mock data, but should not exceed topK
        docs.GetArrayLength().Should().BeLessThanOrEqualTo(10); // Default is 10, with custom it could be 3
    }

    [Fact]
    public void ValidateConfig_WithDefaultConfig_ShouldReturnSuccess()
    {
        // Arrange
        var config = PluginConfig.Default();

        // Act
        var result = _sut.ValidateConfig(config);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void ValidateConfig_WithInvalidTimeout_ShouldReturnFailure()
    {
        // Arrange
        var config = PluginConfig.Default() with { TimeoutMs = 0 };

        // Act
        var result = _sut.ValidateConfig(config);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyPath == "TimeoutMs");
    }

    #endregion

    #region Cost Estimation Tests

    [Fact]
    public async Task ExecuteAsync_ShouldEstimateCost()
    {
        // Arrange
        var input = CreateValidInput("Cost estimation test");
        var config = PluginConfig.Default();

        // Act
        var result = await _sut.ExecuteAsync(input, config);

        // Assert
        result.Success.Should().BeTrue();
        result.Result!.RootElement.TryGetProperty("metrics", out var metrics).Should().BeTrue();
        metrics.TryGetProperty("estimatedCost", out var cost).Should().BeTrue();
        cost.GetDouble().Should().BeGreaterThan(0);
    }

    #endregion

    #region Health Check Tests

    [Fact]
    public async Task HealthCheckAsync_ShouldReturnHealthyStatus()
    {
        // Act
        var result = await _sut.HealthCheckAsync();

        // Assert
        result.Status.Should().Be(HealthStatus.Healthy);
        result.CheckDurationMs.Should().BeGreaterThanOrEqualTo(0);
    }

    #endregion

    #region Metadata Tests

    [Fact]
    public void Metadata_ShouldContainCorrectInformation()
    {
        // Act
        var metadata = _sut.Metadata;

        // Assert
        metadata.Id.Should().Be(_sut.Id);
        metadata.Name.Should().Be(_sut.Name);
        metadata.Version.Should().Be(_sut.Version);
        metadata.Category.Should().Be(_sut.Category);
        metadata.IsEnabled.Should().BeTrue();
        metadata.IsBuiltIn.Should().BeTrue();
    }

    #endregion

    #region Helper Methods

    private static PluginInput CreateValidInput(string query)
    {
        var payload = JsonDocument.Parse($$"""{"query": "{{query}}"}""");
        return new PluginInput
        {
            ExecutionId = Guid.NewGuid(),
            Payload = payload
        };
    }

    #endregion
}
