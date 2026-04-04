using Api.BoundedContexts.KnowledgeBase.Domain.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using FluentAssertions;
using Api.Tests.Constants;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Domain.Services;

/// <summary>
/// Unit tests for ConfidenceValidationService
/// ISSUE-970: BGAI-028 - Confidence validation layer (threshold ≥0.70)
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class ConfidenceValidationServiceTests
{
    private readonly ConfidenceValidationService _service;

    public ConfidenceValidationServiceTests()
    {
        var mockLogger = new Mock<ILogger<ConfidenceValidationService>>();
        _service = new ConfidenceValidationService(mockLogger.Object);
    }

    [Fact]
    public void ConfidenceThreshold_Returns070()
    {
        // Act & Assert
        _service.ConfidenceThreshold.Should().Be(0.70);
    }

    [Fact]
    public void ValidateConfidence_AboveThreshold_ReturnsValid()
    {
        // Arrange
        var confidence = 0.85;

        // Act
        var result = _service.ValidateConfidence(confidence);

        // Assert
        result.IsValid.Should().BeTrue();
        result.ActualConfidence.Should().Be(0.85);
        result.RequiredThreshold.Should().Be(0.70);
        result.Severity.Should().Be(ValidationSeverity.Pass);
        result.ValidationMessage.Should().Contain("meets threshold");
    }

    [Fact]
    public void ValidateConfidence_AtThreshold_ReturnsValid()
    {
        // Arrange
        var confidence = 0.70;

        // Act
        var result = _service.ValidateConfidence(confidence);

        // Assert
        result.IsValid.Should().BeTrue();
        result.ActualConfidence.Should().Be(0.70);
        result.Severity.Should().Be(ValidationSeverity.Pass);
    }

    [Fact]
    public void ValidateConfidence_JustBelowThreshold_ReturnsWarning()
    {
        // Arrange
        var confidence = 0.65;

        // Act
        var result = _service.ValidateConfidence(confidence);

        // Assert
        result.IsValid.Should().BeFalse();
        result.ActualConfidence.Should().Be(0.65);
        result.RequiredThreshold.Should().Be(0.70);
        result.Severity.Should().Be(ValidationSeverity.Warning);
        result.ValidationMessage.Should().Contain("below threshold");
        result.ValidationMessage.Should().Contain("warning");
    }

    [Fact]
    public void ValidateConfidence_CriticallyLow_ReturnsCritical()
    {
        // Arrange
        var confidence = 0.45;

        // Act
        var result = _service.ValidateConfidence(confidence);

        // Assert
        result.IsValid.Should().BeFalse();
        result.ActualConfidence.Should().Be(0.45);
        result.RequiredThreshold.Should().Be(0.70);
        result.Severity.Should().Be(ValidationSeverity.Critical);
        result.ValidationMessage.Should().ContainEquivalentOf("critically low");
    }

    [Fact]
    public void ValidateConfidence_Null_ReturnsUnknown()
    {
        // Act
        var result = _service.ValidateConfidence(null);

        // Assert
        result.IsValid.Should().BeFalse();
        result.ActualConfidence.Should().BeNull();
        result.RequiredThreshold.Should().Be(0.70);
        result.Severity.Should().Be(ValidationSeverity.Unknown);
        result.ValidationMessage.Should().ContainEquivalentOf("No confidence score");
    }

    [Fact]
    public void ValidateConfidence_Zero_ReturnsCritical()
    {
        // Arrange
        var confidence = 0.0;

        // Act
        var result = _service.ValidateConfidence(confidence);

        // Assert
        result.IsValid.Should().BeFalse();
        result.ActualConfidence.Should().Be(0.0);
        result.Severity.Should().Be(ValidationSeverity.Critical);
    }

    [Fact]
    public void ValidateConfidence_Perfect_ReturnsValid()
    {
        // Arrange
        var confidence = 1.0;

        // Act
        var result = _service.ValidateConfidence(confidence);

        // Assert
        result.IsValid.Should().BeTrue();
        result.ActualConfidence.Should().Be(1.0);
        result.Severity.Should().Be(ValidationSeverity.Pass);
    }

    [Fact]
    public void ValidateConfidence_EdgeCase069_ReturnsWarning()
    {
        // Arrange - Just below threshold (0.69)
        var confidence = 0.69;

        // Act
        var result = _service.ValidateConfidence(confidence);

        // Assert
        result.IsValid.Should().BeFalse(); // Below 0.70
        result.Severity.Should().Be(ValidationSeverity.Warning); // Above 0.60
    }

    [Fact]
    public void ValidateConfidence_EdgeCase060_ReturnsWarning()
    {
        // Arrange - At warning threshold boundary
        var confidence = 0.60;

        // Act
        var result = _service.ValidateConfidence(confidence);

        // Assert
        result.IsValid.Should().BeFalse(); // Below 0.70
        result.Severity.Should().Be(ValidationSeverity.Warning); // At 0.60 boundary
    }

    [Fact]
    public void ValidateConfidence_EdgeCase059_ReturnsCritical()
    {
        // Arrange - Just below warning threshold
        var confidence = 0.59;

        // Act
        var result = _service.ValidateConfidence(confidence);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Severity.Should().Be(ValidationSeverity.Critical); // Below 0.60
    }

    // ========== Additional Edge Case Tests (BGAI-031) ==========

    [Fact]
    public void ValidateConfidence_NegativeValue_ReturnsCritical()
    {
        // Arrange - Invalid negative confidence
        var confidence = -0.5;

        // Act
        var result = _service.ValidateConfidence(confidence);

        // Assert
        result.IsValid.Should().BeFalse();
        result.ActualConfidence.Should().Be(-0.5);
        result.Severity.Should().Be(ValidationSeverity.Critical);
        result.ValidationMessage.Should().ContainEquivalentOf("critically low");
    }

    [Fact]
    public void ValidateConfidence_OverOneHundred_ReturnsValid()
    {
        // Arrange - Confidence > 1.0 (edge case, though unusual)
        var confidence = 1.5;

        // Act
        var result = _service.ValidateConfidence(confidence);

        // Assert
        result.IsValid.Should().BeTrue(); // >= 0.70 threshold
        result.ActualConfidence.Should().Be(1.5);
        result.Severity.Should().Be(ValidationSeverity.Pass);
    }

    [Fact]
    public void ValidateConfidence_VeryPreciseDecimal_HandlesCorrectly()
    {
        // Arrange - Test with high precision decimal
        var confidence = 0.7000000001;

        // Act
        var result = _service.ValidateConfidence(confidence);

        // Assert
        result.IsValid.Should().BeTrue();
        result.Severity.Should().Be(ValidationSeverity.Pass);
    }

    [Fact]
    public void ValidateConfidence_JustAboveWarningThreshold_ReturnsWarning()
    {
        // Arrange - 0.6001 (still in warning range)
        var confidence = 0.6001;

        // Act
        var result = _service.ValidateConfidence(confidence);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Severity.Should().Be(ValidationSeverity.Warning);
    }

    [Fact]
    public void ValidateConfidence_MultipleInvocations_ReturnsConsistentResults()
    {
        // Arrange
        var confidence = 0.75;

        // Act - Call multiple times
        var result1 = _service.ValidateConfidence(confidence);
        var result2 = _service.ValidateConfidence(confidence);
        var result3 = _service.ValidateConfidence(confidence);

        // Assert - All results should be identical
        result1.IsValid.Should().BeTrue();
        result2.IsValid.Should().BeTrue();
        result3.IsValid.Should().BeTrue();
        result2.Severity.Should().Be(result1.Severity);
        result3.Severity.Should().Be(result2.Severity);
    }

    [Fact]
    public void ConfidenceValidationResult_AllPropertiesSet()
    {
        // Arrange & Act
        var result = _service.ValidateConfidence(0.80);

        // Assert - Verify all required properties are set
        result.Should().NotBeNull();
        result.ValidationMessage.Should().NotBeNull();
        result.IsValid.Should().NotBe(default(bool));
        result.RequiredThreshold.Should().NotBe(default(double));
        // Severity is set to Pass (enum value 0), which is valid and intentional
        result.Severity.Should().Be(ValidationSeverity.Pass);
    }

    [Theory]
    [InlineData(0.70, ValidationSeverity.Pass)]
    [InlineData(0.71, ValidationSeverity.Pass)]
    [InlineData(0.80, ValidationSeverity.Pass)]
    [InlineData(0.90, ValidationSeverity.Pass)]
    [InlineData(0.99, ValidationSeverity.Pass)]
    [InlineData(1.00, ValidationSeverity.Pass)]
    public void ValidateConfidence_PassRange_AllReturnPass(double confidence, ValidationSeverity expectedSeverity)
    {
        // Act
        var result = _service.ValidateConfidence(confidence);

        // Assert
        result.IsValid.Should().BeTrue();
        result.Severity.Should().Be(expectedSeverity);
    }

    [Theory]
    [InlineData(0.60, ValidationSeverity.Warning)]
    [InlineData(0.61, ValidationSeverity.Warning)]
    [InlineData(0.65, ValidationSeverity.Warning)]
    [InlineData(0.69, ValidationSeverity.Warning)]
    public void ValidateConfidence_WarningRange_AllReturnWarning(double confidence, ValidationSeverity expectedSeverity)
    {
        // Act
        var result = _service.ValidateConfidence(confidence);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Severity.Should().Be(expectedSeverity);
    }

    [Theory]
    [InlineData(0.00, ValidationSeverity.Critical)]
    [InlineData(0.10, ValidationSeverity.Critical)]
    [InlineData(0.30, ValidationSeverity.Critical)]
    [InlineData(0.50, ValidationSeverity.Critical)]
    [InlineData(0.59, ValidationSeverity.Critical)]
    public void ValidateConfidence_CriticalRange_AllReturnCritical(double confidence, ValidationSeverity expectedSeverity)
    {
        // Act
        var result = _service.ValidateConfidence(confidence);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Severity.Should().Be(expectedSeverity);
    }

    // ========== BGAI-038: Floating-Point Precision Edge Cases ==========

    [Fact]
    public void ValidateConfidence_NaN_ReturnsCritical()
    {
        // Arrange - BGAI-038: Test NaN handling
        var confidence = double.NaN;

        // Act
        var result = _service.ValidateConfidence(confidence);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Severity.Should().Be(ValidationSeverity.Critical);
        result.ValidationMessage.Should().ContainEquivalentOf("Invalid confidence value (NaN)");
        double.IsNaN(result.ActualConfidence!.Value).Should().BeTrue();
    }

    [Fact]
    public void ValidateConfidence_PositiveInfinity_ReturnsCritical()
    {
        // Arrange - BGAI-038: Test Positive Infinity handling
        var confidence = double.PositiveInfinity;

        // Act
        var result = _service.ValidateConfidence(confidence);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Severity.Should().Be(ValidationSeverity.Critical);
        result.ValidationMessage.Should().ContainEquivalentOf("Invalid confidence value (Positive Infinity)");
        double.IsPositiveInfinity(result.ActualConfidence!.Value).Should().BeTrue();
    }

    [Fact]
    public void ValidateConfidence_NegativeInfinity_ReturnsCritical()
    {
        // Arrange - BGAI-038: Test Negative Infinity handling
        var confidence = double.NegativeInfinity;

        // Act
        var result = _service.ValidateConfidence(confidence);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Severity.Should().Be(ValidationSeverity.Critical);
        result.ValidationMessage.Should().ContainEquivalentOf("Invalid confidence value (Negative Infinity)");
        double.IsNegativeInfinity(result.ActualConfidence!.Value).Should().BeTrue();
    }

    [Theory]
    [InlineData(0.6999999999999999)] // Just below 0.70 (floating-point precision)
    [InlineData(0.7000000000000001)] // Just above 0.70 (floating-point precision)
    public void ValidateConfidence_FloatingPointPrecisionBoundary_HandlesCorrectly(double confidence)
    {
        // Arrange - BGAI-038: Test epsilon tolerance at threshold boundary
        // With epsilon 1e-10, both values should pass (treated as ~0.70)

        // Act
        var result = _service.ValidateConfidence(confidence);

        // Assert
        result.IsValid.Should().BeTrue();
        result.Severity.Should().Be(ValidationSeverity.Pass);
    }

    [Theory]
    [InlineData(0.5999999999999999)] // Just below 0.60 (floating-point precision)
    [InlineData(0.6000000000000001)] // Just above 0.60 (floating-point precision)
    public void ValidateConfidence_FloatingPointPrecisionWarningBoundary_HandlesCorrectly(double confidence)
    {
        // Arrange - BGAI-038: Test epsilon tolerance at warning threshold boundary
        // With epsilon 1e-10, both values should be in warning range (treated as ~0.60)

        // Act
        var result = _service.ValidateConfidence(confidence);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Severity.Should().Be(ValidationSeverity.Warning);
    }
}

