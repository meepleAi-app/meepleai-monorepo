using System.Threading;
using Api.BoundedContexts.KnowledgeBase.Domain.Services;
using Api.Models;
using Api.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using FluentAssertions;
using Api.Tests.Constants;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Domain.Services;

/// <summary>
/// Unit tests for RagValidationPipelineService
/// ISSUE-977: BGAI-035 - Wire all 5 validation layers in RAG pipeline
/// ISSUE-979: BGAI-037 - Performance optimization (parallel validation)
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class RagValidationPipelineServiceTests
{
    private static CancellationToken TestCancellationToken => TestContext.Current.CancellationToken;

    private readonly Mock<IConfidenceValidationService> _mockConfidenceValidation;
    private readonly Mock<IMultiModelValidationService> _mockMultiModelValidation;
    private readonly Mock<ICitationValidationService> _mockCitationValidation;
    private readonly Mock<IHallucinationDetectionService> _mockHallucinationDetection;
    private readonly Mock<ILogger<RagValidationPipelineService>> _mockLogger;
    private readonly RagValidationPipelineService _service;

    public RagValidationPipelineServiceTests()
    {
        _mockConfidenceValidation = new Mock<IConfidenceValidationService>();
        _mockMultiModelValidation = new Mock<IMultiModelValidationService>();
        _mockCitationValidation = new Mock<ICitationValidationService>();
        _mockHallucinationDetection = new Mock<IHallucinationDetectionService>();
        _mockLogger = new Mock<ILogger<RagValidationPipelineService>>();

        _service = new RagValidationPipelineService(
            _mockConfidenceValidation.Object,
            _mockMultiModelValidation.Object,
            _mockCitationValidation.Object,
            _mockHallucinationDetection.Object,
            _mockLogger.Object);
    }
    [Fact]
    public async Task ValidateResponseAsync_AllLayersPass_ReturnsValid()
    {
        // Arrange
        var response = CreateQaResponse(confidence: 0.85);
        var gameId = Guid.NewGuid().ToString();

        SetupMocksForSuccess();

        // Act
        var result = await _service.ValidateResponseAsync(response, gameId, "en", TestCancellationToken);

        // Assert
        result.IsValid.Should().BeTrue();
        result.LayersPassed.Should().Be(3);
        result.TotalLayers.Should().Be(3);
        result.Severity.Should().Be(RagValidationSeverity.Pass);
        result.Message.Should().ContainEquivalentOf("All validations passed");
        result.MultiModelConsensus.Should().BeNull();
        result.ValidationAccuracyMetrics.Should().BeNull();
    }

    [Fact]
    public async Task ValidateResponseAsync_ConfidenceFails_ReturnsWarning()
    {
        // Arrange
        var response = CreateQaResponse(confidence: 0.55); // Below threshold
        var gameId = Guid.NewGuid().ToString();

        _mockConfidenceValidation
            .Setup(x => x.ValidateConfidence(0.55))
            .Returns(new ConfidenceValidationResult
            {
                IsValid = false,
                ActualConfidence = 0.55,
                RequiredThreshold = 0.70,
                ValidationMessage = "Below threshold",
                Severity = ValidationSeverity.Critical
            });

        _mockCitationValidation
            .Setup(x => x.ValidateCitationsAsync(It.IsAny<IReadOnlyList<Snippet>>(), gameId, default))
            .ReturnsAsync(new CitationValidationResult
            {
                IsValid = true,
                TotalCitations = 2,
                ValidCitations = 2,
                Errors = new List<CitationValidationError>(),
                Message = "All valid"
            });

        _mockHallucinationDetection
            .Setup(x => x.DetectHallucinationsAsync(It.IsAny<string>(), "en", default))
            .ReturnsAsync(new HallucinationValidationResult
            {
                IsValid = true,
                DetectedKeywords = new List<string>(),
                Language = "en",
                TotalKeywordsChecked = 10,
                Message = "No hallucinations",
                Severity = HallucinationSeverity.None
            });

        // Act
        var result = await _service.ValidateResponseAsync(response, gameId, "en", TestCancellationToken);

        // Assert
        result.IsValid.Should().BeFalse();
        result.LayersPassed.Should().Be(2); // Citation and Hallucination pass
        result.TotalLayers.Should().Be(3);
        result.Severity.Should().Be(RagValidationSeverity.Critical);
        result.Message.Should().ContainEquivalentOf("2/3 validations passed");
    }

    [Fact]
    public async Task ValidateResponseAsync_CitationFails_ReturnsWarning()
    {
        // Arrange
        var response = CreateQaResponse(confidence: 0.85);
        var gameId = Guid.NewGuid().ToString();

        _mockConfidenceValidation
            .Setup(x => x.ValidateConfidence(0.85))
            .Returns(new ConfidenceValidationResult
            {
                IsValid = true,
                ActualConfidence = 0.85,
                RequiredThreshold = 0.70,
                ValidationMessage = "Meets threshold",
                Severity = ValidationSeverity.Pass
            });

        _mockCitationValidation
            .Setup(x => x.ValidateCitationsAsync(It.IsAny<IReadOnlyList<Snippet>>(), gameId, default))
            .ReturnsAsync(new CitationValidationResult
            {
                IsValid = false,
                TotalCitations = 2,
                ValidCitations = 1,
                Errors = new List<CitationValidationError>
                {
                    new CitationValidationError
                    {
                        Source = "PDF:123",
                        Page = 999,
                        ErrorMessage = "Invalid page",
                        ErrorType = CitationErrorType.InvalidPageNumber
                    }
                },
                Message = "1/2 valid"
            });

        _mockHallucinationDetection
            .Setup(x => x.DetectHallucinationsAsync(It.IsAny<string>(), "en", default))
            .ReturnsAsync(new HallucinationValidationResult
            {
                IsValid = true,
                DetectedKeywords = new List<string>(),
                Language = "en",
                TotalKeywordsChecked = 10,
                Message = "No hallucinations",
                Severity = HallucinationSeverity.None
            });

        // Act
        var result = await _service.ValidateResponseAsync(response, gameId, "en", TestCancellationToken);

        // Assert
        result.IsValid.Should().BeFalse();
        result.LayersPassed.Should().Be(2);
        result.TotalLayers.Should().Be(3);
        result.Severity.Should().Be(RagValidationSeverity.Warning);
    }

    [Fact]
    public async Task ValidateResponseAsync_HallucinationDetected_ReturnsWarning()
    {
        // Arrange
        var response = CreateQaResponse(confidence: 0.85);
        var gameId = Guid.NewGuid().ToString();

        _mockConfidenceValidation
            .Setup(x => x.ValidateConfidence(0.85))
            .Returns(new ConfidenceValidationResult
            {
                IsValid = true,
                ActualConfidence = 0.85,
                RequiredThreshold = 0.70,
                ValidationMessage = "Meets threshold",
                Severity = ValidationSeverity.Pass
            });

        _mockCitationValidation
            .Setup(x => x.ValidateCitationsAsync(It.IsAny<IReadOnlyList<Snippet>>(), gameId, default))
            .ReturnsAsync(new CitationValidationResult
            {
                IsValid = true,
                TotalCitations = 2,
                ValidCitations = 2,
                Errors = new List<CitationValidationError>(),
                Message = "All valid"
            });

        _mockHallucinationDetection
            .Setup(x => x.DetectHallucinationsAsync(It.IsAny<string>(), "en", default))
            .ReturnsAsync(new HallucinationValidationResult
            {
                IsValid = false,
                DetectedKeywords = new List<string> { "I don't know", "unclear" },
                Language = "en",
                TotalKeywordsChecked = 10,
                Message = "2 keywords detected",
                Severity = HallucinationSeverity.Low
            });

        // Act
        var result = await _service.ValidateResponseAsync(response, gameId, "en", TestCancellationToken);

        // Assert
        result.IsValid.Should().BeFalse();
        result.LayersPassed.Should().Be(2);
        result.TotalLayers.Should().Be(3);
        result.Severity.Should().Be(RagValidationSeverity.Warning);
        result.HallucinationDetection.IsValid.Should().BeFalse();
        result.HallucinationDetection.DetectedKeywords.Count.Should().Be(2);
    }

    [Fact]
    public async Task ValidateResponseAsync_NullResponse_ThrowsArgumentNullException()
    {
        // Act & Assert
        Func<Task> act = async () =>
            await _service.ValidateResponseAsync(null!, "gameId", "en", TestCancellationToken);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task ValidateResponseAsync_EmptyGameId_ThrowsArgumentException()
    {
        // Arrange
        var response = CreateQaResponse(confidence: 0.85);

        // Act & Assert
        Func<Task> act = async () =>
            await _service.ValidateResponseAsync(response, "", "en", TestCancellationToken);
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task ValidateResponseAsync_NullLanguage_DefaultsToEnglish()
    {
        // Arrange
        var response = CreateQaResponse(confidence: 0.85);
        var gameId = Guid.NewGuid().ToString();

        SetupMocksForSuccess();

        // Act
        var result = await _service.ValidateResponseAsync(response, gameId, null, TestCancellationToken);

        // Assert
        result.IsValid.Should().BeTrue();
        _mockHallucinationDetection.Verify(
            x => x.DetectHallucinationsAsync(It.IsAny<string>(), "en", default),
            Times.Once);
    }
    [Fact]
    public async Task ValidateWithMultiModelAsync_AllLayersPass_ReturnsValid()
    {
        // Arrange
        var response = CreateQaResponse(confidence: 0.85);
        var gameId = Guid.NewGuid().ToString();
        var systemPrompt = "You are a helpful assistant";
        var userPrompt = "What are the rules?";

        SetupMocksForMultiModelSuccess();

        // Act
        var result = await _service.ValidateWithMultiModelAsync(
            response, gameId, systemPrompt, userPrompt, "en", TestCancellationToken);

        // Assert
        result.IsValid.Should().BeTrue();
        result.LayersPassed.Should().Be(4);
        result.TotalLayers.Should().Be(4);
        result.Severity.Should().Be(RagValidationSeverity.Pass);
        result.Message.Should().Contain("All validations passed");
        result.MultiModelConsensus.Should().NotBeNull();
        result.MultiModelConsensus.HasConsensus.Should().BeTrue();
        result.ValidationAccuracyMetrics.Should().NotBeNull();
        result.ValidationAccuracyMetrics.Should().Contain("Validation accuracy tracking enabled");
    }

    [Fact]
    public async Task ValidateWithMultiModelAsync_MultiModelFails_ReturnsWarning()
    {
        // Arrange
        var response = CreateQaResponse(confidence: 0.85);
        var gameId = Guid.NewGuid().ToString();
        var systemPrompt = "You are a helpful assistant";
        var userPrompt = "What are the rules?";

        _mockConfidenceValidation
            .Setup(x => x.ValidateConfidence(0.85))
            .Returns(new ConfidenceValidationResult
            {
                IsValid = true,
                ActualConfidence = 0.85,
                RequiredThreshold = 0.70,
                ValidationMessage = "Meets threshold",
                Severity = ValidationSeverity.Pass
            });

        // Multi-model validation fails (no consensus)
        _mockMultiModelValidation
            .Setup(x => x.ValidateWithConsensusAsync(systemPrompt, userPrompt, 0.3, 1000, default))
            .ReturnsAsync(new MultiModelConsensusResult
            {
                HasConsensus = false,
                SimilarityScore = 0.75,
                RequiredThreshold = 0.90,
                Gpt4Response = CreateModelResponse("GPT-4 answer", true),
                ClaudeResponse = CreateModelResponse("Different answer", true),
                ConsensusResponse = null,
                Message = "No consensus",
                TotalDurationMs = 500,
                Severity = ConsensusSeverity.Moderate
            });

        _mockCitationValidation
            .Setup(x => x.ValidateCitationsAsync(It.IsAny<IReadOnlyList<Snippet>>(), gameId, default))
            .ReturnsAsync(new CitationValidationResult
            {
                IsValid = true,
                TotalCitations = 2,
                ValidCitations = 2,
                Errors = new List<CitationValidationError>(),
                Message = "All valid"
            });

        _mockHallucinationDetection
            .Setup(x => x.DetectHallucinationsAsync(It.IsAny<string>(), "en", default))
            .ReturnsAsync(new HallucinationValidationResult
            {
                IsValid = true,
                DetectedKeywords = new List<string>(),
                Language = "en",
                TotalKeywordsChecked = 10,
                Message = "No hallucinations",
                Severity = HallucinationSeverity.None
            });

        // Act
        var result = await _service.ValidateWithMultiModelAsync(
            response, gameId, systemPrompt, userPrompt, "en", TestCancellationToken);

        // Assert
        result.IsValid.Should().BeFalse();
        result.LayersPassed.Should().Be(3); // Confidence, Citation, Hallucination pass
        result.TotalLayers.Should().Be(4);
        result.Severity.Should().Be(RagValidationSeverity.Warning);
        result.MultiModelConsensus.Should().NotBeNull();
        result.MultiModelConsensus.HasConsensus.Should().BeFalse();
    }

    [Fact]
    public async Task ValidateWithMultiModelAsync_UsesConsensusResponseForHallucinationCheck()
    {
        // Arrange
        var response = CreateQaResponse(confidence: 0.85);
        var gameId = Guid.NewGuid().ToString();
        var systemPrompt = "You are a helpful assistant";
        var userPrompt = "What are the rules?";
        var consensusResponse = "This is the consensus answer";

        SetupMocksForMultiModelSuccess(consensusResponse);

        // Act
        var result = await _service.ValidateWithMultiModelAsync(
            response, gameId, systemPrompt, userPrompt, "en", TestCancellationToken);

        // Assert
        result.IsValid.Should().BeTrue();

        // Verify hallucination detection was called with consensus response
        _mockHallucinationDetection.Verify(
            x => x.DetectHallucinationsAsync(consensusResponse, "en", default),
            Times.Once);
    }

    [Fact]
    public async Task ValidateWithMultiModelAsync_NoConsensusResponse_UseOriginalAnswer()
    {
        // Arrange
        var response = CreateQaResponse(confidence: 0.85);
        var gameId = Guid.NewGuid().ToString();
        var systemPrompt = "You are a helpful assistant";
        var userPrompt = "What are the rules?";

        _mockConfidenceValidation
            .Setup(x => x.ValidateConfidence(0.85))
            .Returns(new ConfidenceValidationResult
            {
                IsValid = true,
                ActualConfidence = 0.85,
                RequiredThreshold = 0.70,
                ValidationMessage = "Meets threshold",
                Severity = ValidationSeverity.Pass
            });

        // Multi-model has consensus but no consensus response text
        _mockMultiModelValidation
            .Setup(x => x.ValidateWithConsensusAsync(systemPrompt, userPrompt, 0.3, 1000, default))
            .ReturnsAsync(new MultiModelConsensusResult
            {
                HasConsensus = false,
                SimilarityScore = 0.75,
                RequiredThreshold = 0.90,
                Gpt4Response = CreateModelResponse("GPT-4 answer", true),
                ClaudeResponse = CreateModelResponse("Claude answer", true),
                ConsensusResponse = null, // No consensus response
                Message = "No consensus",
                TotalDurationMs = 500,
                Severity = ConsensusSeverity.Moderate
            });

        _mockCitationValidation
            .Setup(x => x.ValidateCitationsAsync(It.IsAny<IReadOnlyList<Snippet>>(), gameId, default))
            .ReturnsAsync(new CitationValidationResult
            {
                IsValid = true,
                TotalCitations = 2,
                ValidCitations = 2,
                Errors = new List<CitationValidationError>(),
                Message = "All valid"
            });

        _mockHallucinationDetection
            .Setup(x => x.DetectHallucinationsAsync(response.answer, "en", default))
            .ReturnsAsync(new HallucinationValidationResult
            {
                IsValid = true,
                DetectedKeywords = new List<string>(),
                Language = "en",
                TotalKeywordsChecked = 10,
                Message = "No hallucinations",
                Severity = HallucinationSeverity.None
            });

        // Act
        var result = await _service.ValidateWithMultiModelAsync(
            response, gameId, systemPrompt, userPrompt, "en", TestCancellationToken);

        // Assert
        // Verify hallucination detection was called with original answer (not consensus)
        _mockHallucinationDetection.Verify(
            x => x.DetectHallucinationsAsync(response.answer, "en", default),
            Times.Once);
    }

    [Fact]
    public async Task ValidateWithMultiModelAsync_EmptySystemPrompt_ThrowsArgumentException()
    {
        // Arrange
        var response = CreateQaResponse(confidence: 0.85);
        var gameId = Guid.NewGuid().ToString();

        // Act & Assert
        Func<Task> act = () =>
            _service.ValidateWithMultiModelAsync(response, gameId, "", "user prompt", "en", TestCancellationToken);
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task ValidateWithMultiModelAsync_EmptyUserPrompt_ThrowsArgumentException()
    {
        // Arrange
        var response = CreateQaResponse(confidence: 0.85);
        var gameId = Guid.NewGuid().ToString();

        // Act & Assert
        Func<Task> act = () =>
            _service.ValidateWithMultiModelAsync(response, gameId, "system prompt", "", "en", TestCancellationToken);
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task ValidateWithMultiModelAsync_HighHallucinationSeverity_ReturnsCritical()
    {
        // Arrange
        var response = CreateQaResponse(confidence: 0.85);
        var gameId = Guid.NewGuid().ToString();
        var systemPrompt = "You are a helpful assistant";
        var userPrompt = "What are the rules?";

        _mockConfidenceValidation
            .Setup(x => x.ValidateConfidence(0.85))
            .Returns(new ConfidenceValidationResult
            {
                IsValid = true,
                ActualConfidence = 0.85,
                RequiredThreshold = 0.70,
                ValidationMessage = "Meets threshold",
                Severity = ValidationSeverity.Pass
            });

        _mockMultiModelValidation
            .Setup(x => x.ValidateWithConsensusAsync(systemPrompt, userPrompt, 0.3, 1000, default))
            .ReturnsAsync(new MultiModelConsensusResult
            {
                HasConsensus = true,
                SimilarityScore = 0.95,
                RequiredThreshold = 0.90,
                Gpt4Response = CreateModelResponse("Answer", true),
                ClaudeResponse = CreateModelResponse("Answer", true),
                ConsensusResponse = "Answer",
                Message = "Consensus achieved",
                TotalDurationMs = 500,
                Severity = ConsensusSeverity.High
            });

        _mockCitationValidation
            .Setup(x => x.ValidateCitationsAsync(It.IsAny<IReadOnlyList<Snippet>>(), gameId, default))
            .ReturnsAsync(new CitationValidationResult
            {
                IsValid = true,
                TotalCitations = 2,
                ValidCitations = 2,
                Errors = new List<CitationValidationError>(),
                Message = "All valid"
            });

        // High hallucination severity
        _mockHallucinationDetection
            .Setup(x => x.DetectHallucinationsAsync(It.IsAny<string>(), "en", default))
            .ReturnsAsync(new HallucinationValidationResult
            {
                IsValid = false,
                DetectedKeywords = new List<string> { "I don't know", "unclear", "not sure", "cannot find", "not specified" },
                Language = "en",
                TotalKeywordsChecked = 10,
                Message = "5 keywords detected",
                Severity = HallucinationSeverity.High
            });

        // Act
        var result = await _service.ValidateWithMultiModelAsync(
            response, gameId, systemPrompt, userPrompt, "en", TestCancellationToken);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Severity.Should().Be(RagValidationSeverity.Critical);
        result.HallucinationDetection.Severity.Should().Be(HallucinationSeverity.High);
    }
    private QaResponse CreateQaResponse(double confidence)
    {
        return new QaResponse(
            answer: "This is a test answer",
            snippets: new[]
            {
                new Snippet("Snippet 1", "PDF:123", 1, 0, 0.9f),
                new Snippet("Snippet 2", "PDF:123", 2, 0, 0.8f)
            },
            promptTokens: 100,
            completionTokens: 50,
            totalTokens: 150,
            confidence: confidence,
            metadata: null
        );
    }

    private ModelResponse CreateModelResponse(string response, bool success)
    {
        return new ModelResponse
        {
            ModelId = "test-model",
            ResponseText = response,
            IsSuccess = success,
            ErrorMessage = success ? null : "Error",
            DurationMs = 100,
            Usage = new LlmUsage(50, 25, 75)
        };
    }

    private void SetupMocksForSuccess()
    {
        _mockConfidenceValidation
            .Setup(x => x.ValidateConfidence(It.IsAny<double?>()))
            .Returns(new ConfidenceValidationResult
            {
                IsValid = true,
                ActualConfidence = 0.85,
                RequiredThreshold = 0.70,
                ValidationMessage = "Meets threshold",
                Severity = ValidationSeverity.Pass
            });

        _mockCitationValidation
            .Setup(x => x.ValidateCitationsAsync(It.IsAny<IReadOnlyList<Snippet>>(), It.IsAny<string>(), default))
            .ReturnsAsync(new CitationValidationResult
            {
                IsValid = true,
                TotalCitations = 2,
                ValidCitations = 2,
                Errors = new List<CitationValidationError>(),
                Message = "All citations valid"
            });

        _mockHallucinationDetection
            .Setup(x => x.DetectHallucinationsAsync(It.IsAny<string>(), It.IsAny<string>(), default))
            .ReturnsAsync(new HallucinationValidationResult
            {
                IsValid = true,
                DetectedKeywords = new List<string>(),
                Language = "en",
                TotalKeywordsChecked = 10,
                Message = "No hallucinations detected",
                Severity = HallucinationSeverity.None
            });
    }

    private void SetupMocksForMultiModelSuccess(string? consensusResponse = "Consensus answer")
    {
        SetupMocksForSuccess();

        _mockMultiModelValidation
            .Setup(x => x.ValidateWithConsensusAsync(It.IsAny<string>(), It.IsAny<string>(), 0.3, 1000, default))
            .ReturnsAsync(new MultiModelConsensusResult
            {
                HasConsensus = true,
                SimilarityScore = 0.95,
                RequiredThreshold = 0.90,
                Gpt4Response = CreateModelResponse("GPT-4 answer", true),
                ClaudeResponse = CreateModelResponse("Claude answer", true),
                ConsensusResponse = consensusResponse,
                Message = "Consensus achieved",
                TotalDurationMs = 500,
                Severity = ConsensusSeverity.High
            });

        // Override hallucination setup to use consensus response if provided
        if (!string.IsNullOrWhiteSpace(consensusResponse))
        {
            _mockHallucinationDetection
                .Setup(x => x.DetectHallucinationsAsync(consensusResponse, It.IsAny<string>(), default))
                .ReturnsAsync(new HallucinationValidationResult
                {
                    IsValid = true,
                    DetectedKeywords = new List<string>(),
                    Language = "en",
                    TotalKeywordsChecked = 10,
                    Message = "No hallucinations detected",
                    Severity = HallucinationSeverity.None
                });
        }
    }
}

