// =============================================================================
// MeepleAI - RAG Plugin System
// Issue #3357 - Sentence Window RAG Strategy Tests
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
/// Unit tests for RetrievalSentenceWindowPlugin.
/// Issue #3357: Sentence Window RAG Strategy implementation.
/// </summary>
[Trait("Category", "Unit")]
[Trait("BoundedContext", "KnowledgeBase")]
[Trait("Component", "Plugins")]
public class RetrievalSentenceWindowPluginTests
{
    private readonly RetrievalSentenceWindowPlugin _sut;
    private readonly Mock<ILogger<RetrievalSentenceWindowPlugin>> _loggerMock;

    public RetrievalSentenceWindowPluginTests()
    {
        _loggerMock = new Mock<ILogger<RetrievalSentenceWindowPlugin>>();
        _sut = new RetrievalSentenceWindowPlugin(_loggerMock.Object);
    }

    #region Contract Verification Tests

    [Fact]
    public void Plugin_ShouldHaveCorrectId()
    {
        // Assert
        _sut.Id.Should().Be("retrieval-sentence-window-v1");
    }

    [Fact]
    public void Plugin_ShouldHaveCorrectName()
    {
        // Assert
        _sut.Name.Should().Be("Sentence Window Retrieval");
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
        propsElement.TryGetProperty("windowSize", out _).Should().BeTrue();
        propsElement.TryGetProperty("minScore", out _).Should().BeTrue();
        propsElement.TryGetProperty("collection", out _).Should().BeTrue();
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
    public async Task ExecuteAsync_ShouldReturnDocumentsInResult()
    {
        // Arrange
        var input = CreateValidInput("What are the movement rules?");
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
        var input = CreateValidInput("How does trading work?");
        var config = PluginConfig.Default();

        // Act
        var result = await _sut.ExecuteAsync(input, config);

        // Assert
        result.Success.Should().BeTrue();
        result.Result!.RootElement.TryGetProperty("metrics", out var metrics).Should().BeTrue();
        metrics.TryGetProperty("sentencesRetrieved", out _).Should().BeTrue();
        metrics.TryGetProperty("windowsExpanded", out _).Should().BeTrue();
        metrics.TryGetProperty("documentsAfterMerge", out _).Should().BeTrue();
        metrics.TryGetProperty("totalTokens", out _).Should().BeTrue();
        metrics.TryGetProperty("windowSize", out _).Should().BeTrue();
    }

    [Fact]
    public async Task ExecuteAsync_ShouldRecordExecutionMetrics()
    {
        // Arrange
        var input = CreateValidInput("What are the special abilities?");
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

    #region Configuration Tests

    [Fact]
    public async Task ExecuteAsync_WithCustomWindowSize_ShouldUseConfiguredValue()
    {
        // Arrange
        var input = CreateValidInput("Test query");
        var customConfig = JsonDocument.Parse("""{"windowSize": 3}""");
        var config = PluginConfig.Default() with { CustomConfig = customConfig };

        // Act
        var result = await _sut.ExecuteAsync(input, config);

        // Assert
        result.Success.Should().BeTrue();
        result.Result!.RootElement.TryGetProperty("metrics", out var metrics).Should().BeTrue();
        metrics.TryGetProperty("windowSize", out var windowSize).Should().BeTrue();
        windowSize.GetInt32().Should().Be(3);
    }

    [Fact]
    public async Task ExecuteAsync_WithCustomTopK_ShouldLimitResults()
    {
        // Arrange
        var input = CreateValidInput("Test query");
        var customConfig = JsonDocument.Parse("""{"topK": 3}""");
        var config = PluginConfig.Default() with { CustomConfig = customConfig };

        // Act
        var result = await _sut.ExecuteAsync(input, config);

        // Assert
        result.Success.Should().BeTrue();
        result.Result!.RootElement.TryGetProperty("documents", out var docs).Should().BeTrue();
        docs.GetArrayLength().Should().BeLessThanOrEqualTo(3);
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
