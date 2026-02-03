using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.SharedKernel.Domain.Exceptions;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Domain.ValueObjects;

/// <summary>
/// Tests for the Confidence value object.
/// Issue #3025: Backend 90% Coverage Target - Phase 18
/// </summary>
[Trait("Category", "Unit")]
public sealed class ConfidenceTests
{
    #region Constructor Tests

    [Theory]
    [InlineData(0.0)]
    [InlineData(0.5)]
    [InlineData(1.0)]
    public void Constructor_WithValidValue_CreatesConfidence(double value)
    {
        // Act
        var confidence = new Confidence(value);

        // Assert
        confidence.Value.Should().Be(value);
    }

    [Fact]
    public void Constructor_WithNegativeValue_ThrowsValidationException()
    {
        // Act
        var action = () => new Confidence(-0.1);

        // Assert
        action.Should().Throw<ValidationException>()
            .WithMessage("*Confidence must be between 0.0 and 1.0*");
    }

    [Fact]
    public void Constructor_WithValueGreaterThanOne_ThrowsValidationException()
    {
        // Act
        var action = () => new Confidence(1.1);

        // Assert
        action.Should().Throw<ValidationException>()
            .WithMessage("*Confidence must be between 0.0 and 1.0*");
    }

    [Fact]
    public void Constructor_WithNaN_ThrowsValidationException()
    {
        // Act
        var action = () => new Confidence(double.NaN);

        // Assert
        action.Should().Throw<ValidationException>()
            .WithMessage("*Confidence cannot be NaN or Infinity*");
    }

    [Fact]
    public void Constructor_WithPositiveInfinity_ThrowsValidationException()
    {
        // Act
        var action = () => new Confidence(double.PositiveInfinity);

        // Assert - infinity is caught by range check first
        action.Should().Throw<ValidationException>();
    }

    [Fact]
    public void Constructor_WithNegativeInfinity_ThrowsValidationException()
    {
        // Act
        var action = () => new Confidence(double.NegativeInfinity);

        // Assert
        action.Should().Throw<ValidationException>();
    }

    [Fact]
    public void Constructor_WithTinyNegativeRoundingError_ClampsToZero()
    {
        // Arrange - value slightly below 0 due to floating-point rounding
        var value = -1e-10;

        // Act
        var confidence = new Confidence(value);

        // Assert
        confidence.Value.Should().Be(0.0);
    }

    [Fact]
    public void Constructor_WithTinyPositiveRoundingError_ClampsToOne()
    {
        // Arrange - value slightly above 1 due to floating-point rounding
        var value = 1.0 + 1e-10;

        // Act
        var confidence = new Confidence(value);

        // Assert
        confidence.Value.Should().Be(1.0);
    }

    #endregion

    #region IsHigh/IsMedium/IsLow Tests

    [Theory]
    [InlineData(0.8)]
    [InlineData(0.9)]
    [InlineData(1.0)]
    public void IsHigh_WithHighValue_ReturnsTrue(double value)
    {
        // Arrange
        var confidence = new Confidence(value);

        // Assert
        confidence.IsHigh().Should().BeTrue();
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(0.5)]
    [InlineData(0.79)]
    public void IsHigh_WithNonHighValue_ReturnsFalse(double value)
    {
        // Arrange
        var confidence = new Confidence(value);

        // Assert
        confidence.IsHigh().Should().BeFalse();
    }

    [Theory]
    [InlineData(0.5)]
    [InlineData(0.6)]
    [InlineData(0.79)]
    public void IsMedium_WithMediumValue_ReturnsTrue(double value)
    {
        // Arrange
        var confidence = new Confidence(value);

        // Assert
        confidence.IsMedium().Should().BeTrue();
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(0.49)]
    [InlineData(0.8)]
    [InlineData(1.0)]
    public void IsMedium_WithNonMediumValue_ReturnsFalse(double value)
    {
        // Arrange
        var confidence = new Confidence(value);

        // Assert
        confidence.IsMedium().Should().BeFalse();
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(0.3)]
    [InlineData(0.49)]
    public void IsLow_WithLowValue_ReturnsTrue(double value)
    {
        // Arrange
        var confidence = new Confidence(value);

        // Assert
        confidence.IsLow().Should().BeTrue();
    }

    [Theory]
    [InlineData(0.5)]
    [InlineData(0.8)]
    [InlineData(1.0)]
    public void IsLow_WithNonLowValue_ReturnsFalse(double value)
    {
        // Arrange
        var confidence = new Confidence(value);

        // Assert
        confidence.IsLow().Should().BeFalse();
    }

    #endregion

    #region Static Instances Tests

    [Fact]
    public void High_HasCorrectValue()
    {
        // Assert
        Confidence.High.Value.Should().Be(0.9);
        Confidence.High.IsHigh().Should().BeTrue();
    }

    [Fact]
    public void Medium_HasCorrectValue()
    {
        // Assert
        Confidence.Medium.Value.Should().Be(0.7);
        Confidence.Medium.IsMedium().Should().BeTrue();
    }

    [Fact]
    public void Low_HasCorrectValue()
    {
        // Assert
        Confidence.Low.Value.Should().Be(0.3);
        Confidence.Low.IsLow().Should().BeTrue();
    }

    [Fact]
    public void Zero_HasCorrectValue()
    {
        // Assert
        Confidence.Zero.Value.Should().Be(0.0);
        Confidence.Zero.IsLow().Should().BeTrue();
    }

    #endregion

    #region Equality Tests

    [Fact]
    public void Equals_WithSameValue_ReturnsTrue()
    {
        // Arrange
        var confidence1 = new Confidence(0.75);
        var confidence2 = new Confidence(0.75);

        // Assert
        confidence1.Should().Be(confidence2);
    }

    [Fact]
    public void Equals_WithDifferentValue_ReturnsFalse()
    {
        // Arrange
        var confidence1 = new Confidence(0.75);
        var confidence2 = new Confidence(0.76);

        // Assert
        confidence1.Should().NotBe(confidence2);
    }

    [Fact]
    public void GetHashCode_WithSameValue_ReturnsSameHash()
    {
        // Arrange
        var confidence1 = new Confidence(0.75);
        var confidence2 = new Confidence(0.75);

        // Assert
        confidence1.GetHashCode().Should().Be(confidence2.GetHashCode());
    }

    #endregion

    #region Implicit Conversion Tests

    [Fact]
    public void ImplicitConversion_ToDouble_ReturnsValue()
    {
        // Arrange
        var confidence = new Confidence(0.85);

        // Act
        double value = confidence;

        // Assert
        value.Should().Be(0.85);
    }

    #endregion

    #region Parse Tests

    [Fact]
    public void Parse_WithValidValue_ReturnsConfidence()
    {
        // Act
        var confidence = Confidence.Parse(0.75);

        // Assert
        confidence.Value.Should().Be(0.75);
    }

    [Fact]
    public void Parse_WithInvalidValue_ThrowsValidationException()
    {
        // Act
        var action = () => Confidence.Parse(1.5);

        // Assert
        action.Should().Throw<ValidationException>();
    }

    #endregion

    #region ToString Tests

    [Fact]
    public void ToString_ReturnsPercentageFormat()
    {
        // Arrange
        var confidence = new Confidence(0.85);

        // Act
        var result = confidence.ToString();

        // Assert
        result.Should().Be("85%");
    }

    [Fact]
    public void ToString_WithZero_ReturnsZeroPercent()
    {
        // Arrange
        var confidence = new Confidence(0.0);

        // Act
        var result = confidence.ToString();

        // Assert
        result.Should().Be("0%");
    }

    [Fact]
    public void ToString_WithOne_Returns100Percent()
    {
        // Arrange
        var confidence = new Confidence(1.0);

        // Act
        var result = confidence.ToString();

        // Assert
        result.Should().Be("100%");
    }

    #endregion

    #region ToDouble Tests

    [Fact]
    public void ToDouble_ThrowsNotSupportedException()
    {
        // Arrange
        var confidence = new Confidence(0.5);

        // Act
        var action = () => confidence.ToDouble();

        // Assert
        action.Should().Throw<NotSupportedException>()
            .WithMessage("*Use implicit conversion to double instead*");
    }

    #endregion
}
