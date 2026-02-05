// =============================================================================
// MeepleAI - RAG Plugin System
// Issue #3360 - Step-Back Prompting Strategy Tests
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
/// Unit tests for RetrievalStepBackPlugin.
/// Issue #3360: Step-Back Prompting Strategy implementation.
/// </summary>
[Trait("Category", "Unit")]
[Trait("BoundedContext", "KnowledgeBase")]
[Trait("Component", "Plugins")]
public class RetrievalStepBackPluginTests
{
    private readonly RetrievalStepBackPlugin _sut;
    private readonly Mock<ILogger<RetrievalStepBackPlugin>> _loggerMock;

    public RetrievalStepBackPluginTests()
    {
        _loggerMock = new Mock<ILogger<RetrievalStepBackPlugin>>();
        _sut = new RetrievalStepBackPlugin(_loggerMock.Object);
    }

    #region Contract Verification Tests

    [Fact]
    public void Plugin_ShouldHaveCorrectId()
    {
        // Assert
        _sut.Id.Should().Be("retrieval-step-back-v1");
    }

    [Fact]
    public void Plugin_ShouldHaveCorrectName()
    {
        // Assert
        _sut.Name.Should().Be("Step-Back Prompting Retrieval");
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
        propsElement.TryGetProperty("abstractQuestion", out _).Should().BeTrue();
        propsElement.TryGetProperty("stepBackApplied", out _).Should().BeTrue();
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
        propsElement.TryGetProperty("abstractTopK", out _).Should().BeTrue();
        propsElement.TryGetProperty("minScore", out _).Should().BeTrue();
    }

    [Fact]
    public void Metadata_ShouldContainCorrectInformation()
    {
        // Act
        var metadata = _sut.Metadata;

        // Assert
        metadata.Should().NotBeNull();
        metadata.Id.Should().Be(_sut.Id);
        metadata.Name.Should().Be(_sut.Name);
        metadata.Version.Should().Be(_sut.Version);
        metadata.Category.Should().Be(_sut.Category);
        metadata.IsEnabled.Should().BeTrue();
        metadata.IsBuiltIn.Should().BeTrue();
    }

    #endregion

    #region Input Validation Tests

    [Fact]
    public void ValidateInput_WithValidQuery_ShouldReturnSuccess()
    {
        // Arrange
        var input = CreateValidInput("What are the movement rules?");

        // Act
        var result = _sut.ValidateInput(input);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void ValidateInput_WithMissingQuery_ShouldReturnFailure()
    {
        // Arrange
        var payload = JsonDocument.Parse("""{"context": "some context"}""");
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
        var payload = JsonDocument.Parse("""
            {
                "query": "How does combat work?",
                "context": "We are playing a strategy game"
            }
            """);
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
        var input = CreateValidInput("What are the combat mechanics?");
        var config = PluginConfig.Default();

        // Act
        var result = await _sut.ExecuteAsync(input, config);

        // Assert
        result.Success.Should().BeTrue();
        result.Result!.RootElement.TryGetProperty("answer", out var answer).Should().BeTrue();
        answer.GetString().Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnAbstractQuestion()
    {
        // Arrange
        var input = CreateValidInput("How does trading work?");
        var config = PluginConfig.Default();

        // Act
        var result = await _sut.ExecuteAsync(input, config);

        // Assert
        result.Success.Should().BeTrue();
        result.Result!.RootElement.TryGetProperty("abstractQuestion", out var abstractQ).Should().BeTrue();
        abstractQ.GetString().Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_ShouldHaveStepBackAppliedFlag()
    {
        // Arrange
        var input = CreateValidInput("What are the special abilities?");
        var config = PluginConfig.Default();

        // Act
        var result = await _sut.ExecuteAsync(input, config);

        // Assert
        result.Success.Should().BeTrue();
        result.Result!.RootElement.TryGetProperty("stepBackApplied", out var stepBack).Should().BeTrue();
        stepBack.GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnConfidenceScore()
    {
        // Arrange
        var input = CreateValidInput("How does resource management work?");
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
        var input = CreateValidInput("How does movement work?");
        var config = PluginConfig.Default();

        // Act
        var result = await _sut.ExecuteAsync(input, config);

        // Assert
        result.Success.Should().BeTrue();
        result.Result!.RootElement.TryGetProperty("metrics", out var metrics).Should().BeTrue();
        metrics.TryGetProperty("totalTokens", out _).Should().BeTrue();
        metrics.TryGetProperty("inputTokens", out _).Should().BeTrue();
        metrics.TryGetProperty("outputTokens", out _).Should().BeTrue();
        metrics.TryGetProperty("broadContextChunks", out _).Should().BeTrue();
        metrics.TryGetProperty("focusedContextChunks", out _).Should().BeTrue();
        metrics.TryGetProperty("documentsRetrieved", out _).Should().BeTrue();
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
        result.Success.Should().BeTrue();
        result.Metrics.DurationMs.Should().BeGreaterThan(0);
        // Items processed is tracked in result.metrics.documentsRetrieved
        result.Result!.RootElement.TryGetProperty("metrics", out var metrics).Should().BeTrue();
        metrics.TryGetProperty("documentsRetrieved", out var docs).Should().BeTrue();
        docs.GetInt32().Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldCalculateConfidenceScore()
    {
        // Arrange
        var input = CreateValidInput("How do turn order rules work?");
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
        var payload = JsonDocument.Parse("""{"notQuery": "test"}""");
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

    #region Step-Back Specific Tests

    [Fact]
    public async Task ExecuteAsync_ShouldGenerateAbstractQuestion()
    {
        // Arrange
        var input = CreateValidInput("What happens when I land on a property in Monopoly?");
        var config = PluginConfig.Default();

        // Act
        var result = await _sut.ExecuteAsync(input, config);

        // Assert
        result.Success.Should().BeTrue();
        result.Result!.RootElement.TryGetProperty("abstractQuestion", out var abstractQ).Should().BeTrue();
        var abstractQuestion = abstractQ.GetString();
        abstractQuestion.Should().NotBeNull();
        // The plugin randomly selects from: "general rules for", "core mechanics of", "fundamental concepts in", "basic principles of"
        var validPatterns = new[] { "general rules for", "core mechanics of", "fundamental concepts in", "basic principles of" };
        validPatterns.Should().Contain(p => abstractQuestion!.Contains(p, StringComparison.Ordinal));
    }

    [Fact]
    public async Task ExecuteAsync_ShouldIncludeBroadAndFocusedContext()
    {
        // Arrange
        var input = CreateValidInput("How does card combat work?");
        var config = PluginConfig.Default();

        // Act
        var result = await _sut.ExecuteAsync(input, config);

        // Assert
        result.Success.Should().BeTrue();
        result.Result!.RootElement.TryGetProperty("metrics", out var metrics).Should().BeTrue();

        metrics.TryGetProperty("broadContextChunks", out var broadChunks).Should().BeTrue();
        broadChunks.GetInt32().Should().BeGreaterThan(0);

        metrics.TryGetProperty("focusedContextChunks", out var focusedChunks).Should().BeTrue();
        focusedChunks.GetInt32().Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task ExecuteAsync_AnswerShouldContainBroadContext()
    {
        // Arrange
        var input = CreateValidInput("What are the dice mechanics?");
        var config = PluginConfig.Default();

        // Act
        var result = await _sut.ExecuteAsync(input, config);

        // Assert
        result.Success.Should().BeTrue();
        result.Result!.RootElement.TryGetProperty("answer", out var answer).Should().BeTrue();
        var answerText = answer.GetString();
        answerText.Should().Contain("Broader Context");
        answerText.Should().Contain("Specific Answer");
    }

    #endregion

    #region Configuration Tests

    [Fact]
    public async Task ExecuteAsync_WithCustomTopK_ShouldLimitResults()
    {
        // Arrange
        var input = CreateValidInput("Test query");
        using var customConfig = JsonDocument.Parse("""{"topK": 5, "abstractTopK": 3}""");
        var config = PluginConfig.Create(
            timeoutMs: 30000,
            customConfig: customConfig);

        // Act
        var result = await _sut.ExecuteAsync(input, config);

        // Assert
        result.Success.Should().BeTrue(
            $"Plugin execution failed. Error: {result.ErrorMessage ?? "none"}, Code: {result.ErrorCode ?? "none"}");
        result.Result!.RootElement.TryGetProperty("documents", out var docs).Should().BeTrue();
        docs.GetArrayLength().Should().BeLessThanOrEqualTo(13); // topK + abstractTopK max
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
        var result = await _sut.HealthCheckAsync(CancellationToken.None);

        // Assert
        result.Status.Should().Be(HealthStatus.Healthy);
        result.CheckDurationMs.Should().BeGreaterThanOrEqualTo(0);
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
