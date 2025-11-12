using Api.BoundedContexts.KnowledgeBase.Domain.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Domain.Services;

/// <summary>
/// Unit tests for ConfidenceValidationService
/// ISSUE-970: BGAI-028 - Confidence validation layer (threshold ≥0.70)
/// </summary>
public class ConfidenceValidationServiceTests
{
    private readonly ConfidenceValidationService _service;

    public ConfidenceValidationServiceTests()
    {
        var mockLogger = new Mock<ILogger<ConfidenceValidationService>>();
        _service = new ConfidenceValidationService(mockLogger.Object);
    }

    [Fact]
    public void Test01_ConfidenceThreshold_Returns070()
    {
        // Act & Assert
        Assert.Equal(0.70, _service.ConfidenceThreshold);
    }

    [Fact]
    public void Test02_ValidateConfidence_AboveThreshold_ReturnsValid()
    {
        // Arrange
        var confidence = 0.85;

        // Act
        var result = _service.ValidateConfidence(confidence);

        // Assert
        Assert.True(result.IsValid);
        Assert.Equal(0.85, result.ActualConfidence);
        Assert.Equal(0.70, result.RequiredThreshold);
        Assert.Equal(ValidationSeverity.Pass, result.Severity);
        Assert.Contains("meets threshold", result.ValidationMessage);
    }

    [Fact]
    public void Test03_ValidateConfidence_AtThreshold_ReturnsValid()
    {
        // Arrange
        var confidence = 0.70;

        // Act
        var result = _service.ValidateConfidence(confidence);

        // Assert
        Assert.True(result.IsValid);
        Assert.Equal(0.70, result.ActualConfidence);
        Assert.Equal(ValidationSeverity.Pass, result.Severity);
    }

    [Fact]
    public void Test04_ValidateConfidence_JustBelowThreshold_ReturnsWarning()
    {
        // Arrange
        var confidence = 0.65;

        // Act
        var result = _service.ValidateConfidence(confidence);

        // Assert
        Assert.False(result.IsValid);
        Assert.Equal(0.65, result.ActualConfidence);
        Assert.Equal(0.70, result.RequiredThreshold);
        Assert.Equal(ValidationSeverity.Warning, result.Severity);
        Assert.Contains("below threshold", result.ValidationMessage);
        Assert.Contains("warning", result.ValidationMessage);
    }

    [Fact]
    public void Test05_ValidateConfidence_CriticallyLow_ReturnsCritical()
    {
        // Arrange
        var confidence = 0.45;

        // Act
        var result = _service.ValidateConfidence(confidence);

        // Assert
        Assert.False(result.IsValid);
        Assert.Equal(0.45, result.ActualConfidence);
        Assert.Equal(0.70, result.RequiredThreshold);
        Assert.Equal(ValidationSeverity.Critical, result.Severity);
        Assert.Contains("critically low", result.ValidationMessage);
    }

    [Fact]
    public void Test06_ValidateConfidence_Null_ReturnsUnknown()
    {
        // Act
        var result = _service.ValidateConfidence(null);

        // Assert
        Assert.False(result.IsValid);
        Assert.Null(result.ActualConfidence);
        Assert.Equal(0.70, result.RequiredThreshold);
        Assert.Equal(ValidationSeverity.Unknown, result.Severity);
        Assert.Contains("No confidence score", result.ValidationMessage);
    }

    [Fact]
    public void Test07_ValidateConfidence_Zero_ReturnsCritical()
    {
        // Arrange
        var confidence = 0.0;

        // Act
        var result = _service.ValidateConfidence(confidence);

        // Assert
        Assert.False(result.IsValid);
        Assert.Equal(0.0, result.ActualConfidence);
        Assert.Equal(ValidationSeverity.Critical, result.Severity);
    }

    [Fact]
    public void Test08_ValidateConfidence_Perfect_ReturnsValid()
    {
        // Arrange
        var confidence = 1.0;

        // Act
        var result = _service.ValidateConfidence(confidence);

        // Assert
        Assert.True(result.IsValid);
        Assert.Equal(1.0, result.ActualConfidence);
        Assert.Equal(ValidationSeverity.Pass, result.Severity);
    }

    [Fact]
    public void Test09_ValidateConfidence_EdgeCase069_ReturnsWarning()
    {
        // Arrange - Just below threshold (0.69)
        var confidence = 0.69;

        // Act
        var result = _service.ValidateConfidence(confidence);

        // Assert
        Assert.False(result.IsValid); // Below 0.70
        Assert.Equal(ValidationSeverity.Warning, result.Severity); // Above 0.60
    }

    [Fact]
    public void Test10_ValidateConfidence_EdgeCase060_ReturnsWarning()
    {
        // Arrange - At warning threshold boundary
        var confidence = 0.60;

        // Act
        var result = _service.ValidateConfidence(confidence);

        // Assert
        Assert.False(result.IsValid); // Below 0.70
        Assert.Equal(ValidationSeverity.Warning, result.Severity); // At 0.60 boundary
    }

    [Fact]
    public void Test11_ValidateConfidence_EdgeCase059_ReturnsCritical()
    {
        // Arrange - Just below warning threshold
        var confidence = 0.59;

        // Act
        var result = _service.ValidateConfidence(confidence);

        // Assert
        Assert.False(result.IsValid);
        Assert.Equal(ValidationSeverity.Critical, result.Severity); // Below 0.60
    }
}
