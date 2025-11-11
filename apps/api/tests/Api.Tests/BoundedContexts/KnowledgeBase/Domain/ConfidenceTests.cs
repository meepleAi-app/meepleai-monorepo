using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.SharedKernel.Domain.Exceptions;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Domain;

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

    [Fact]
    public void IsHigh_WithHighConfidence_ReturnsTrue()
    {
        // Arrange
        var confidence = new Confidence(0.9);

        // Act & Assert
        Assert.True(confidence.IsHigh());
        Assert.False(confidence.IsMedium());
        Assert.False(confidence.IsLow());
    }

    [Fact]
    public void IsMedium_WithMediumConfidence_ReturnsTrue()
    {
        // Arrange
        var confidence = new Confidence(0.6);

        // Act & Assert
        Assert.False(confidence.IsHigh());
        Assert.True(confidence.IsMedium());
        Assert.False(confidence.IsLow());
    }

    [Fact]
    public void IsLow_WithLowConfidence_ReturnsTrue()
    {
        // Arrange
        var confidence = new Confidence(0.3);

        // Act & Assert
        Assert.False(confidence.IsHigh());
        Assert.False(confidence.IsMedium());
        Assert.True(confidence.IsLow());
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
