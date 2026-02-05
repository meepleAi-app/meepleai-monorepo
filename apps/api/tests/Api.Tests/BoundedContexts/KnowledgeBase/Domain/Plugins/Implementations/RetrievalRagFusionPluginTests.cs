// =============================================================================
// MeepleAI - RAG Plugin Tests
// Issue #3363 - RAG-Fusion Strategy Tests
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
/// Unit tests for RetrievalRagFusionPlugin.
/// Issue #3363: RAG-Fusion Strategy implementation.
/// </summary>
[Trait("Category", "Unit")]
[Trait("BoundedContext", "KnowledgeBase")]
[Trait("Component", "Plugins")]
public class RetrievalRagFusionPluginTests
{
    private readonly RetrievalRagFusionPlugin _sut;
    private readonly Mock<ILogger<RetrievalRagFusionPlugin>> _loggerMock;

    public RetrievalRagFusionPluginTests()
    {
        _loggerMock = new Mock<ILogger<RetrievalRagFusionPlugin>>();
        _sut = new RetrievalRagFusionPlugin(_loggerMock.Object);
    }

    #region Contract Verification Tests

    [Fact]
    public void Plugin_ShouldHaveCorrectId()
    {
        // Assert
        _sut.Id.Should().Be("retrieval-rag-fusion-v1");
    }

    [Fact]
    public void Plugin_ShouldHaveCorrectName()
    {
        // Assert
        _sut.Name.Should().Be("RAG-Fusion Retrieval");
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
        propsElement.TryGetProperty("queryVariations", out _).Should().BeTrue();
        propsElement.TryGetProperty("documentsRetrieved", out _).Should().BeTrue();
        propsElement.TryGetProperty("totalDocumentsBeforeFusion", out _).Should().BeTrue();
    }

    [Fact]
    public void Plugin_ShouldHaveValidConfigSchema()
    {
        // Act
        var schema = _sut.ConfigSchema;

        // Assert
        schema.Should().NotBeNull();
        schema.RootElement.TryGetProperty("properties", out var propsElement).Should().BeTrue();
        propsElement.TryGetProperty("defaultTopK", out _).Should().BeTrue();
        propsElement.TryGetProperty("rrfConstant", out _).Should().BeTrue();
        propsElement.TryGetProperty("defaultQueryVariations", out _).Should().BeTrue();
    }

    [Fact]
    public void ConfigSchema_ShouldDefineRrfConstantDefault()
    {
        // Act
        var schema = _sut.ConfigSchema;
        var properties = schema.RootElement.GetProperty("properties");

        // Assert
        properties.TryGetProperty("rrfConstant", out var rrfConst).Should().BeTrue();
        rrfConst.GetProperty("default").GetInt32().Should().Be(60);
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
        var input = CreateValidInput("How do movement rules work?");

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

    [Fact]
    public void ValidateInput_WithInvalidTopK_TooLow_ShouldReturnFailure()
    {
        // Arrange
        var payload = JsonDocument.Parse("""{"query": "test", "topK": 0}""");
        var input = new PluginInput
        {
            ExecutionId = Guid.NewGuid(),
            Payload = payload
        };

        // Act
        var result = _sut.ValidateInput(input);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Code == "INVALID_TOPK");
    }

    [Fact]
    public void ValidateInput_WithInvalidTopK_TooHigh_ShouldReturnFailure()
    {
        // Arrange
        var payload = JsonDocument.Parse("""{"query": "test", "topK": 100}""");
        var input = new PluginInput
        {
            ExecutionId = Guid.NewGuid(),
            Payload = payload
        };

        // Act
        var result = _sut.ValidateInput(input);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Code == "INVALID_TOPK");
    }

    [Fact]
    public void ValidateInput_WithInvalidMinScore_ShouldReturnFailure()
    {
        // Arrange
        var payload = JsonDocument.Parse("""{"query": "test", "minScore": 1.5}""");
        var input = new PluginInput
        {
            ExecutionId = Guid.NewGuid(),
            Payload = payload
        };

        // Act
        var result = _sut.ValidateInput(input);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Code == "INVALID_MINSCORE");
    }

    [Fact]
    public void ValidateInput_WithInvalidQueryVariations_TooLow_ShouldReturnFailure()
    {
        // Arrange
        var payload = JsonDocument.Parse("""{"query": "test", "queryVariations": 1}""");
        var input = new PluginInput
        {
            ExecutionId = Guid.NewGuid(),
            Payload = payload
        };

        // Act
        var result = _sut.ValidateInput(input);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Code == "INVALID_VARIATIONS");
    }

    [Fact]
    public void ValidateInput_WithInvalidQueryVariations_TooHigh_ShouldReturnFailure()
    {
        // Arrange
        var payload = JsonDocument.Parse("""{"query": "test", "queryVariations": 15}""");
        var input = new PluginInput
        {
            ExecutionId = Guid.NewGuid(),
            Payload = payload
        };

        // Act
        var result = _sut.ValidateInput(input);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Code == "INVALID_VARIATIONS");
    }

    [Fact]
    public void ValidateInput_WithValidConfiguration_ShouldReturnSuccess()
    {
        // Arrange
        var payload = JsonDocument.Parse("""
            {
                "query": "How do I score points?",
                "topK": 15,
                "minScore": 0.6,
                "queryVariations": 5
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
        var input = CreateValidInput("How do I win the game?");
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
        var input = CreateValidInput("What are the victory conditions?");
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
        var input = CreateValidInput("How does combat resolve?");
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
    public async Task ExecuteAsync_ShouldReturnQueryVariations()
    {
        // Arrange
        var input = CreateValidInput("How does combat work?");
        var config = PluginConfig.Default();

        // Act
        var result = await _sut.ExecuteAsync(input, config);

        // Assert
        result.Success.Should().BeTrue();
        result.Result!.RootElement.TryGetProperty("queryVariations", out var variations).Should().BeTrue();
        variations.GetArrayLength().Should().BeGreaterThanOrEqualTo(2);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldIncludeOriginalQueryInVariations()
    {
        // Arrange
        var originalQuery = "What are the basic rules?";
        var input = CreateValidInput(originalQuery);
        var config = PluginConfig.Default();

        // Act
        var result = await _sut.ExecuteAsync(input, config);

        // Assert
        result.Success.Should().BeTrue();
        var variations = result.Result!.RootElement.GetProperty("queryVariations")
            .EnumerateArray()
            .Select(e => e.GetString())
            .ToList();
        variations.Should().Contain(originalQuery);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnFusedDocuments()
    {
        // Arrange
        var input = CreateValidInput("Explain the scoring system");
        var config = PluginConfig.Default();

        // Act
        var result = await _sut.ExecuteAsync(input, config);

        // Assert
        result.Success.Should().BeTrue();
        result.Result!.RootElement.TryGetProperty("documentsRetrieved", out var docsRetrieved).Should().BeTrue();
        docsRetrieved.GetInt32().Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnTotalBeforeFusion()
    {
        // Arrange
        var input = CreateValidInput("How do turns work?");
        var config = PluginConfig.Default();

        // Act
        var result = await _sut.ExecuteAsync(input, config);

        // Assert
        result.Success.Should().BeTrue();
        result.Result!.RootElement.TryGetProperty("totalDocumentsBeforeFusion", out var totalBefore).Should().BeTrue();
        result.Result!.RootElement.TryGetProperty("documentsRetrieved", out var afterFusion).Should().BeTrue();

        // Before fusion should typically be >= after fusion due to deduplication
        totalBefore.GetInt32().Should().BeGreaterThanOrEqualTo(afterFusion.GetInt32());
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnChunksWithFusedScore()
    {
        // Arrange
        var input = CreateValidInput("What is the win condition?");
        var config = PluginConfig.Default();

        // Act
        var result = await _sut.ExecuteAsync(input, config);

        // Assert
        result.Success.Should().BeTrue();
        result.Result!.RootElement.TryGetProperty("chunks", out var chunks).Should().BeTrue();
        var chunkArray = chunks.EnumerateArray().ToList();
        chunkArray.Should().NotBeEmpty();

        foreach (var chunk in chunkArray)
        {
            chunk.TryGetProperty("fusedScore", out var fusedScore).Should().BeTrue();
            fusedScore.GetDouble().Should().BeGreaterThan(0);
        }
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnMetrics()
    {
        // Arrange
        var input = CreateValidInput("How to play?");
        var config = PluginConfig.Default();

        // Act
        var result = await _sut.ExecuteAsync(input, config);

        // Assert
        result.Success.Should().BeTrue();
        result.Result!.RootElement.TryGetProperty("metrics", out var metrics).Should().BeTrue();
        metrics.TryGetProperty("tokensEstimate", out _).Should().BeTrue();
        metrics.TryGetProperty("rrfConstant", out _).Should().BeTrue();
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnRrfConstant()
    {
        // Arrange
        var input = CreateValidInput("Game rules");
        var config = PluginConfig.Default();

        // Act
        var result = await _sut.ExecuteAsync(input, config);

        // Assert
        result.Success.Should().BeTrue();
        var rrfConstant = result.Result!.RootElement
            .GetProperty("metrics")
            .GetProperty("rrfConstant")
            .GetInt32();
        rrfConstant.Should().Be(60); // Default RRF constant
    }

    [Fact]
    public async Task ExecuteAsync_WithContext_ShouldIncludeInAnswer()
    {
        // Arrange
        var payload = JsonDocument.Parse("""
            {
                "query": "How do I move pieces?",
                "context": "Chess-like game with hexagonal board"
            }
            """);
        var input = new PluginInput
        {
            ExecutionId = Guid.NewGuid(),
            Payload = payload
        };
        var config = PluginConfig.Default();

        // Act
        var result = await _sut.ExecuteAsync(input, config);

        // Assert
        result.Success.Should().BeTrue();
        var answer = result.Result!.RootElement.GetProperty("answer").GetString();
        answer.Should().Contain("Context");
    }

    [Fact]
    public async Task ExecuteAsync_ShouldRecordExecutionMetrics()
    {
        // Arrange
        var input = CreateValidInput("Explain resources");
        var config = PluginConfig.Default();

        // Act
        var result = await _sut.ExecuteAsync(input, config);

        // Assert
        result.Success.Should().BeTrue();
        result.Metrics.DurationMs.Should().BeGreaterThanOrEqualTo(0);
        result.Result!.RootElement.TryGetProperty("metrics", out var metrics).Should().BeTrue();
        metrics.GetProperty("tokensEstimate").GetInt32().Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task ExecuteAsync_CustomVariationCount_ShouldGenerateRequestedNumber()
    {
        // Arrange
        var payload = JsonDocument.Parse("""{"query": "Rules overview", "queryVariations": 6}""");
        var input = new PluginInput
        {
            ExecutionId = Guid.NewGuid(),
            Payload = payload
        };
        var config = PluginConfig.Default();

        // Act
        var result = await _sut.ExecuteAsync(input, config);

        // Assert
        result.Success.Should().BeTrue();
        var variationsCount = result.Result!.RootElement.GetProperty("variationsCount").GetInt32();
        variationsCount.Should().Be(6);
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

    #region RAG-Fusion Specific Tests

    [Fact]
    public async Task ExecuteAsync_ShouldGenerateDiverseQueryVariations()
    {
        // Arrange
        var payload = JsonDocument.Parse("""{"query": "How does combat resolve?", "queryVariations": 5}""");
        var input = new PluginInput
        {
            ExecutionId = Guid.NewGuid(),
            Payload = payload
        };
        var config = PluginConfig.Default();

        // Act
        var result = await _sut.ExecuteAsync(input, config);

        // Assert
        result.Success.Should().BeTrue();
        var variations = result.Result!.RootElement.GetProperty("queryVariations")
            .EnumerateArray()
            .Select(e => e.GetString())
            .ToList();

        // All variations should be distinct
        variations.Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public async Task ExecuteAsync_FusionShouldReduceDocuments()
    {
        // Arrange
        var payload = JsonDocument.Parse("""{"query": "Scoring rules", "queryVariations": 5}""");
        var input = new PluginInput
        {
            ExecutionId = Guid.NewGuid(),
            Payload = payload
        };
        var config = PluginConfig.Default();

        // Act
        var result = await _sut.ExecuteAsync(input, config);

        // Assert
        result.Success.Should().BeTrue();
        var totalBefore = result.Result!.RootElement.GetProperty("totalDocumentsBeforeFusion").GetInt32();
        var afterFusion = result.Result!.RootElement.GetProperty("documentsRetrieved").GetInt32();

        // Fusion should typically reduce document count due to deduplication
        afterFusion.Should().BeLessThanOrEqualTo(totalBefore);
    }

    [Fact]
    public async Task ExecuteAsync_ChunksShouldBeOrderedByFusedScore()
    {
        // Arrange
        var input = CreateValidInput("Victory conditions");
        var config = PluginConfig.Default();

        // Act
        var result = await _sut.ExecuteAsync(input, config);

        // Assert
        result.Success.Should().BeTrue();
        var chunks = result.Result!.RootElement.GetProperty("chunks")
            .EnumerateArray()
            .Select(c => c.GetProperty("fusedScore").GetDouble())
            .ToList();

        // Chunks should be ordered by descending fused score
        chunks.Should().BeInDescendingOrder();
    }

    [Fact]
    public async Task ExecuteAsync_VariationsShouldCoverDifferentPerspectives()
    {
        // Arrange
        var payload = JsonDocument.Parse("""{"query": "How to play", "queryVariations": 4}""");
        var input = new PluginInput
        {
            ExecutionId = Guid.NewGuid(),
            Payload = payload
        };
        var config = PluginConfig.Default();

        // Act
        var result = await _sut.ExecuteAsync(input, config);

        // Assert
        result.Success.Should().BeTrue();
        var variations = result.Result!.RootElement.GetProperty("queryVariations")
            .EnumerateArray()
            .Select(e => e.GetString()!)
            .ToList();

        // Should have the original query and different types of variations
        var hasOriginal = variations.Any(v => v.Equals("How to play", StringComparison.OrdinalIgnoreCase));
        var hasReformulation = variations.Any(v =>
            v.Contains("beginner", StringComparison.OrdinalIgnoreCase) ||
            v.Contains("rules", StringComparison.OrdinalIgnoreCase) ||
            v.Contains("strategy", StringComparison.OrdinalIgnoreCase) ||
            v.Contains("Regarding", StringComparison.OrdinalIgnoreCase) ||
            v.Contains("Help", StringComparison.OrdinalIgnoreCase) ||
            v.Contains("Explain", StringComparison.OrdinalIgnoreCase));

        hasOriginal.Should().BeTrue("Original query should be included");
        hasReformulation.Should().BeTrue("Reformulated variations should be generated");
    }

    #endregion

    #region Configuration Tests

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

    [Fact]
    public async Task HealthCheckAsync_ShouldReturnMessage()
    {
        // Act
        var result = await _sut.HealthCheckAsync(CancellationToken.None);

        // Assert
        result.Message.Should().NotBeNullOrEmpty();
        result.Message.Should().Contain("healthy");
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
