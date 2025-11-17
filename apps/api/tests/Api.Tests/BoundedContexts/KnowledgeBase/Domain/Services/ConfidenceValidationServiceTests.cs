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

    // ========== Additional Edge Case Tests (BGAI-031) ==========

    [Fact]
    public void Test12_ValidateConfidence_NegativeValue_ReturnsCritical()
    {
        // Arrange - Invalid negative confidence
        var confidence = -0.5;

        // Act
        var result = _service.ValidateConfidence(confidence);

        // Assert
        Assert.False(result.IsValid);
        Assert.Equal(-0.5, result.ActualConfidence);
        Assert.Equal(ValidationSeverity.Critical, result.Severity);
        Assert.Contains("critically low", result.ValidationMessage);
    }

    [Fact]
    public void Test13_ValidateConfidence_OverOneHundred_ReturnsValid()
    {
        // Arrange - Confidence > 1.0 (edge case, though unusual)
        var confidence = 1.5;

        // Act
        var result = _service.ValidateConfidence(confidence);

        // Assert
        Assert.True(result.IsValid); // >= 0.70 threshold
        Assert.Equal(1.5, result.ActualConfidence);
        Assert.Equal(ValidationSeverity.Pass, result.Severity);
    }

    [Fact]
    public void Test14_ValidateConfidence_VeryPreciseDecimal_HandlesCorrectly()
    {
        // Arrange - Test with high precision decimal
        var confidence = 0.7000000001;

        // Act
        var result = _service.ValidateConfidence(confidence);

        // Assert
        Assert.True(result.IsValid);
        Assert.Equal(ValidationSeverity.Pass, result.Severity);
    }

    [Fact]
    public void Test15_ValidateConfidence_JustAboveWarningThreshold_ReturnsWarning()
    {
        // Arrange - 0.6001 (still in warning range)
        var confidence = 0.6001;

        // Act
        var result = _service.ValidateConfidence(confidence);

        // Assert
        Assert.False(result.IsValid);
        Assert.Equal(ValidationSeverity.Warning, result.Severity);
    }

    [Fact]
    public void Test16_ValidateConfidence_MultipleInvocations_ReturnsConsistentResults()
    {
        // Arrange
        var confidence = 0.75;

        // Act - Call multiple times
        var result1 = _service.ValidateConfidence(confidence);
        var result2 = _service.ValidateConfidence(confidence);
        var result3 = _service.ValidateConfidence(confidence);

        // Assert - All results should be identical
        Assert.True(result1.IsValid);
        Assert.True(result2.IsValid);
        Assert.True(result3.IsValid);
        Assert.Equal(result1.Severity, result2.Severity);
        Assert.Equal(result2.Severity, result3.Severity);
    }

    [Fact]
    public void Test17_ConfidenceValidationResult_AllPropertiesSet()
    {
        // Arrange & Act
        var result = _service.ValidateConfidence(0.80);

        // Assert - Verify all required properties are set
        Assert.NotNull(result);
        Assert.NotNull(result.ValidationMessage);
        Assert.NotEqual(default(bool), result.IsValid);
        Assert.NotEqual(default(double), result.RequiredThreshold);
        Assert.NotEqual(default(ValidationSeverity), result.Severity);
    }

    [Theory]
    [InlineData(0.70, ValidationSeverity.Pass)]
    [InlineData(0.71, ValidationSeverity.Pass)]
    [InlineData(0.80, ValidationSeverity.Pass)]
    [InlineData(0.90, ValidationSeverity.Pass)]
    [InlineData(0.99, ValidationSeverity.Pass)]
    [InlineData(1.00, ValidationSeverity.Pass)]
    public void Test18_ValidateConfidence_PassRange_AllReturnPass(double confidence, ValidationSeverity expectedSeverity)
    {
        // Act
        var result = _service.ValidateConfidence(confidence);

        // Assert
        Assert.True(result.IsValid);
        Assert.Equal(expectedSeverity, result.Severity);
    }

    [Theory]
    [InlineData(0.60, ValidationSeverity.Warning)]
    [InlineData(0.61, ValidationSeverity.Warning)]
    [InlineData(0.65, ValidationSeverity.Warning)]
    [InlineData(0.69, ValidationSeverity.Warning)]
    public void Test19_ValidateConfidence_WarningRange_AllReturnWarning(double confidence, ValidationSeverity expectedSeverity)
    {
        // Act
        var result = _service.ValidateConfidence(confidence);

        // Assert
        Assert.False(result.IsValid);
        Assert.Equal(expectedSeverity, result.Severity);
    }

    [Theory]
    [InlineData(0.00, ValidationSeverity.Critical)]
    [InlineData(0.10, ValidationSeverity.Critical)]
    [InlineData(0.30, ValidationSeverity.Critical)]
    [InlineData(0.50, ValidationSeverity.Critical)]
    [InlineData(0.59, ValidationSeverity.Critical)]
    public void Test20_ValidateConfidence_CriticalRange_AllReturnCritical(double confidence, ValidationSeverity expectedSeverity)
    {
        // Act
        var result = _service.ValidateConfidence(confidence);

        // Assert
        Assert.False(result.IsValid);
        Assert.Equal(expectedSeverity, result.Severity);
    }
}
