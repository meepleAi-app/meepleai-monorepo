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
    private readonly Mock<ICitationValidationService> _mockCitationValidation;
    private readonly Mock<IHallucinationDetectionService> _mockHallucinationDetection;
    private readonly Mock<ILogger<RagValidationPipelineService>> _mockLogger;
    private readonly RagValidationPipelineService _service;

    public RagValidationPipelineServiceTests()
    {
        _mockConfidenceValidation = new Mock<IConfidenceValidationService>();
        _mockCitationValidation = new Mock<ICitationValidationService>();
        _mockHallucinationDetection = new Mock<IHallucinationDetectionService>();
        _mockLogger = new Mock<ILogger<RagValidationPipelineService>>();

        _service = new RagValidationPipelineService(
            _mockConfidenceValidation.Object,
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
}
