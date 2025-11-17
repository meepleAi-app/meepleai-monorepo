using Api.BoundedContexts.KnowledgeBase.Domain.Services;
using Api.Models;
using Api.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Domain.Services;

/// <summary>
/// Unit tests for RagValidationPipelineService
/// ISSUE-977: BGAI-035 - Wire all 5 validation layers in RAG pipeline
/// ISSUE-979: BGAI-037 - Performance optimization (parallel validation)
/// </summary>
public class RagValidationPipelineServiceTests
{
    private readonly Mock<IConfidenceValidationService> _mockConfidenceValidation;
    private readonly Mock<IMultiModelValidationService> _mockMultiModelValidation;
    private readonly Mock<ICitationValidationService> _mockCitationValidation;
    private readonly Mock<IHallucinationDetectionService> _mockHallucinationDetection;
    private readonly Mock<ValidationAccuracyTrackingService> _mockAccuracyTracking;
    private readonly Mock<ILogger<RagValidationPipelineService>> _mockLogger;
    private readonly RagValidationPipelineService _service;

    public RagValidationPipelineServiceTests()
    {
        _mockConfidenceValidation = new Mock<IConfidenceValidationService>();
        _mockMultiModelValidation = new Mock<IMultiModelValidationService>();
        _mockCitationValidation = new Mock<ICitationValidationService>();
        _mockHallucinationDetection = new Mock<IHallucinationDetectionService>();
        _mockAccuracyTracking = new Mock<ValidationAccuracyTrackingService>();
        _mockLogger = new Mock<ILogger<RagValidationPipelineService>>();

        _service = new RagValidationPipelineService(
            _mockConfidenceValidation.Object,
            _mockMultiModelValidation.Object,
            _mockCitationValidation.Object,
            _mockHallucinationDetection.Object,
            _mockAccuracyTracking.Object,
            _mockLogger.Object);
    }

    #region ValidateResponseAsync Tests (Standard Mode - 3 Layers)

    [Fact]
    public async Task Test01_ValidateResponseAsync_AllLayersPass_ReturnsValid()
    {
        // Arrange
        var response = CreateQaResponse(confidence: 0.85);
        var gameId = Guid.NewGuid().ToString();

        SetupMocksForSuccess();

        // Act
        var result = await _service.ValidateResponseAsync(response, gameId, "en");

        // Assert
        Assert.True(result.IsValid);
        Assert.Equal(3, result.LayersPassed);
        Assert.Equal(3, result.TotalLayers);
        Assert.Equal(RagValidationSeverity.Pass, result.Severity);
        Assert.Contains("All validations passed", result.Message);
        Assert.Null(result.MultiModelConsensus);
        Assert.Null(result.ValidationAccuracyMetrics);
    }

    [Fact]
    public async Task Test02_ValidateResponseAsync_ConfidenceFails_ReturnsWarning()
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
        var result = await _service.ValidateResponseAsync(response, gameId, "en");

        // Assert
        Assert.False(result.IsValid);
        Assert.Equal(2, result.LayersPassed); // Citation and Hallucination pass
        Assert.Equal(3, result.TotalLayers);
        Assert.Equal(RagValidationSeverity.Critical, result.Severity);
        Assert.Contains("2/3 validations passed", result.Message);
    }

    [Fact]
    public async Task Test03_ValidateResponseAsync_CitationFails_ReturnsWarning()
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
        var result = await _service.ValidateResponseAsync(response, gameId, "en");

        // Assert
        Assert.False(result.IsValid);
        Assert.Equal(2, result.LayersPassed);
        Assert.Equal(3, result.TotalLayers);
        Assert.Equal(RagValidationSeverity.Warning, result.Severity);
    }

    [Fact]
    public async Task Test04_ValidateResponseAsync_HallucinationDetected_ReturnsWarning()
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
        var result = await _service.ValidateResponseAsync(response, gameId, "en");

        // Assert
        Assert.False(result.IsValid);
        Assert.Equal(2, result.LayersPassed);
        Assert.Equal(3, result.TotalLayers);
        Assert.Equal(RagValidationSeverity.Warning, result.Severity);
        Assert.False(result.HallucinationDetection.IsValid);
        Assert.Equal(2, result.HallucinationDetection.DetectedKeywords.Count);
    }

    [Fact]
    public async Task Test05_ValidateResponseAsync_NullResponse_ThrowsArgumentNullException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            _service.ValidateResponseAsync(null!, "gameId", "en"));
    }

    [Fact]
    public async Task Test06_ValidateResponseAsync_EmptyGameId_ThrowsArgumentException()
    {
        // Arrange
        var response = CreateQaResponse(confidence: 0.85);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.ValidateResponseAsync(response, "", "en"));
    }

    [Fact]
    public async Task Test07_ValidateResponseAsync_NullLanguage_DefaultsToEnglish()
    {
        // Arrange
        var response = CreateQaResponse(confidence: 0.85);
        var gameId = Guid.NewGuid().ToString();

        SetupMocksForSuccess();

        // Act
        var result = await _service.ValidateResponseAsync(response, gameId, null);

        // Assert
        Assert.True(result.IsValid);
        _mockHallucinationDetection.Verify(
            x => x.DetectHallucinationsAsync(It.IsAny<string>(), "en", default),
            Times.Once);
    }

    #endregion

    #region ValidateWithMultiModelAsync Tests (Multi-Model Mode - 4 Layers)

    [Fact]
    public async Task Test08_ValidateWithMultiModelAsync_AllLayersPass_ReturnsValid()
    {
        // Arrange
        var response = CreateQaResponse(confidence: 0.85);
        var gameId = Guid.NewGuid().ToString();
        var systemPrompt = "You are a helpful assistant";
        var userPrompt = "What are the rules?";

        SetupMocksForMultiModelSuccess();

        // Act
        var result = await _service.ValidateWithMultiModelAsync(
            response, gameId, systemPrompt, userPrompt, "en");

        // Assert
        Assert.True(result.IsValid);
        Assert.Equal(4, result.LayersPassed);
        Assert.Equal(4, result.TotalLayers);
        Assert.Equal(RagValidationSeverity.Pass, result.Severity);
        Assert.Contains("All validations passed", result.Message);
        Assert.NotNull(result.MultiModelConsensus);
        Assert.True(result.MultiModelConsensus.HasConsensus);
        Assert.NotNull(result.ValidationAccuracyMetrics);
        Assert.Contains("Validation accuracy tracking enabled", result.ValidationAccuracyMetrics);
    }

    [Fact]
    public async Task Test09_ValidateWithMultiModelAsync_MultiModelFails_ReturnsWarning()
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
            response, gameId, systemPrompt, userPrompt, "en");

        // Assert
        Assert.False(result.IsValid);
        Assert.Equal(3, result.LayersPassed); // Confidence, Citation, Hallucination pass
        Assert.Equal(4, result.TotalLayers);
        Assert.Equal(RagValidationSeverity.Warning, result.Severity);
        Assert.NotNull(result.MultiModelConsensus);
        Assert.False(result.MultiModelConsensus.HasConsensus);
    }

    [Fact]
    public async Task Test10_ValidateWithMultiModelAsync_UsesConsensusResponseForHallucinationCheck()
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
            response, gameId, systemPrompt, userPrompt, "en");

        // Assert
        Assert.True(result.IsValid);

        // Verify hallucination detection was called with consensus response
        _mockHallucinationDetection.Verify(
            x => x.DetectHallucinationsAsync(consensusResponse, "en", default),
            Times.Once);
    }

    [Fact]
    public async Task Test11_ValidateWithMultiModelAsync_NoConsensusResponse_UseOriginalAnswer()
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
            response, gameId, systemPrompt, userPrompt, "en");

        // Assert
        // Verify hallucination detection was called with original answer (not consensus)
        _mockHallucinationDetection.Verify(
            x => x.DetectHallucinationsAsync(response.answer, "en", default),
            Times.Once);
    }

    [Fact]
    public async Task Test12_ValidateWithMultiModelAsync_EmptySystemPrompt_ThrowsArgumentException()
    {
        // Arrange
        var response = CreateQaResponse(confidence: 0.85);
        var gameId = Guid.NewGuid().ToString();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.ValidateWithMultiModelAsync(response, gameId, "", "user prompt", "en"));
    }

    [Fact]
    public async Task Test13_ValidateWithMultiModelAsync_EmptyUserPrompt_ThrowsArgumentException()
    {
        // Arrange
        var response = CreateQaResponse(confidence: 0.85);
        var gameId = Guid.NewGuid().ToString();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.ValidateWithMultiModelAsync(response, gameId, "system prompt", "", "en"));
    }

    [Fact]
    public async Task Test14_ValidateWithMultiModelAsync_HighHallucinationSeverity_ReturnsCritical()
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
            response, gameId, systemPrompt, userPrompt, "en");

        // Assert
        Assert.False(result.IsValid);
        Assert.Equal(RagValidationSeverity.Critical, result.Severity);
        Assert.Equal(HallucinationSeverity.High, result.HallucinationDetection.Severity);
    }

    #endregion

    #region Helper Methods

    private QaResponse CreateQaResponse(double confidence)
    {
        return new QaResponse(
            answer: "This is a test answer",
            snippets: new[]
            {
                new Snippet("Snippet 1", "PDF:123", 1, 0, 0.9),
                new Snippet("Snippet 2", "PDF:123", 2, 0, 0.8)
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

    #endregion
}
