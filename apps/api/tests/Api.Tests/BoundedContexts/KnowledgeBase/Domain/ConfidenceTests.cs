using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.SharedKernel.Domain.Exceptions;
using Xunit;
using Api.Tests.Constants;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Domain;

[Trait("Category", TestCategories.Unit)]

public class ConfidenceTests
{
    [Theory]
    [InlineData(0.0)]
    [InlineData(0.5)]
    [InlineData(1.0)]
    public void Confidence_WithValidValue_CreatesSuccessfully(double value)
    {
        // Act
        var confidence = new Confidence(value);

        // Assert
        Assert.Equal(value, confidence.Value);
    }

    [Theory]
    [InlineData(-0.1)]
    [InlineData(1.1)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void Confidence_WithInvalidValue_ThrowsValidationException(double value)
    {
        // Act & Assert
        Assert.Throws<ValidationException>(() => new Confidence(value));
    }

    [Theory]
    [InlineData(0.9, true, false, false)]   // High confidence
    [InlineData(0.6, false, true, false)]   // Medium confidence
    [InlineData(0.3, false, false, true)]   // Low confidence
    public void ConfidenceLevel_ReturnsCorrectClassification(double value, bool expectedIsHigh, bool expectedIsMedium, bool expectedIsLow)
    {
        // Arrange
        var confidence = new Confidence(value);

        // Act & Assert
        Assert.Equal(expectedIsHigh, confidence.IsHigh());
        Assert.Equal(expectedIsMedium, confidence.IsMedium());
        Assert.Equal(expectedIsLow, confidence.IsLow());
    }

    [Fact]
    public void ImplicitConversion_ToDouble_WorksCorrectly()
    {
        // Arrange
        var confidence = new Confidence(0.75);

        // Act
        double value = confidence;

        // Assert
        Assert.Equal(0.75, value);
    }
}
