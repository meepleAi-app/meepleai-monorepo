using Api.SharedKernel.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace Api.Tests.SharedKernel.Domain.ValueObjects;

[Trait("Category", "Unit")]
public sealed class PercentageTests
{
    #region Create Factory Tests

    [Theory]
    [InlineData(0)]
    [InlineData(50)]
    [InlineData(100)]
    [InlineData(99.9)]
    [InlineData(0.1)]
    public void Create_WithValidValue_CreatesPercentage(decimal value)
    {
        // Act
        var percentage = Percentage.Create(value);

        // Assert
        percentage.Value.Should().Be(value);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(-0.01)]
    [InlineData(-100)]
    public void Create_WithNegativeValue_ThrowsArgumentOutOfRangeException(decimal negativeValue)
    {
        // Act & Assert
        var action = () => Percentage.Create(negativeValue);
        action.Should().Throw<ArgumentOutOfRangeException>()
            .WithMessage("*Percentage must be between 0 and 100*");
    }

    [Theory]
    [InlineData(100.01)]
    [InlineData(101)]
    [InlineData(200)]
    public void Create_WithValueOverHundred_ThrowsArgumentOutOfRangeException(decimal overValue)
    {
        // Act & Assert
        var action = () => Percentage.Create(overValue);
        action.Should().Throw<ArgumentOutOfRangeException>()
            .WithMessage("*Percentage must be between 0 and 100*");
    }

    #endregion

    #region FromRatio Tests

    [Theory]
    [InlineData(0.0, 0)]
    [InlineData(0.5, 50)]
    [InlineData(1.0, 100)]
    [InlineData(0.25, 25)]
    [InlineData(0.755, 75.5)]
    public void FromRatio_WithValidRatio_ConvertsToPercentage(decimal ratio, decimal expectedPercentage)
    {
        // Act
        var percentage = Percentage.FromRatio(ratio);

        // Assert
        percentage.Value.Should().Be(expectedPercentage);
    }

    [Theory]
    [InlineData(-0.01)]
    [InlineData(-1)]
    public void FromRatio_WithNegativeRatio_ThrowsArgumentOutOfRangeException(decimal negativeRatio)
    {
        // Act & Assert
        var action = () => Percentage.FromRatio(negativeRatio);
        action.Should().Throw<ArgumentOutOfRangeException>()
            .WithMessage("*Ratio must be between 0.0 and 1.0*");
    }

    [Theory]
    [InlineData(1.01)]
    [InlineData(2)]
    public void FromRatio_WithRatioOverOne_ThrowsArgumentOutOfRangeException(decimal overRatio)
    {
        // Act & Assert
        var action = () => Percentage.FromRatio(overRatio);
        action.Should().Throw<ArgumentOutOfRangeException>()
            .WithMessage("*Ratio must be between 0.0 and 1.0*");
    }

    #endregion

    #region FromFraction Tests

    [Theory]
    [InlineData(1, 2, 50)]
    [InlineData(3, 4, 75)]
    [InlineData(0, 10, 0)]
    [InlineData(10, 10, 100)]
    [InlineData(1, 3, 33.333333333333333333333333333)]
    public void FromFraction_WithValidFraction_CalculatesPercentage(int count, int total, decimal expected)
    {
        // Act
        var percentage = Percentage.FromFraction(count, total);

        // Assert
        percentage.Value.Should().BeApproximately(expected, 0.0001m);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-100)]
    public void FromFraction_WithZeroOrNegativeTotal_ReturnsZero(int invalidTotal)
    {
        // Act
        var percentage = Percentage.FromFraction(5, invalidTotal);

        // Assert
        percentage.Value.Should().Be(0);
    }

    [Fact]
    public void FromFraction_WithCountExceedingTotal_ClampsTohundred()
    {
        // Arrange - Count > Total (e.g., 150%)
        var count = 15;
        var total = 10;

        // Act
        var percentage = Percentage.FromFraction(count, total);

        // Assert - Should be clamped to 100
        percentage.Value.Should().Be(100);
    }

    [Fact]
    public void FromFraction_WithNegativeCount_ClampsToZero()
    {
        // Arrange - Negative count would give negative percentage
        var count = -5;
        var total = 10;

        // Act
        var percentage = Percentage.FromFraction(count, total);

        // Assert - Should be clamped to 0
        percentage.Value.Should().Be(0);
    }

    #endregion

    #region Static Instances Tests

    [Fact]
    public void Zero_ReturnsZeroPercentage()
    {
        // Act
        var zero = Percentage.Zero;

        // Assert
        zero.Value.Should().Be(0);
    }

    [Fact]
    public void OneHundred_ReturnsHundredPercentage()
    {
        // Act
        var hundred = Percentage.OneHundred;

        // Assert
        hundred.Value.Should().Be(100);
    }

    #endregion

    #region ToRatio Tests

    [Theory]
    [InlineData(0, 0.0)]
    [InlineData(50, 0.5)]
    [InlineData(100, 1.0)]
    [InlineData(25, 0.25)]
    [InlineData(75.5, 0.755)]
    public void ToRatio_ConvertsToRatio(decimal percentageValue, decimal expectedRatio)
    {
        // Arrange
        var percentage = Percentage.Create(percentageValue);

        // Act
        var ratio = percentage.ToRatio();

        // Assert
        ratio.Should().Be(expectedRatio);
    }

    #endregion

    #region ToString Tests

    [Fact]
    public void ToString_ReturnsFormattedPercentage()
    {
        // Arrange
        var percentage = Percentage.Create(75.5m);

        // Act
        var result = percentage.ToString();

        // Assert - Use culture-independent check: contains percentage and value
        result.Should().EndWith("%");
        result.Should().Contain("75");
        result.Should().Contain("5");
    }

    [Fact]
    public void ToString_WithZeroDecimals_ReturnsFormattedPercentage()
    {
        // Arrange
        var percentage = Percentage.Create(50m);

        // Act
        var result = percentage.ToString(0);

        // Assert - Culture-independent: "50%" is the same regardless of locale
        result.Should().Be("50%");
    }

    [Fact]
    public void ToString_WithTwoDecimals_ReturnsFormattedPercentage()
    {
        // Arrange
        var percentage = Percentage.Create(33.333m);

        // Act
        var result = percentage.ToString(2);

        // Assert - Use culture-independent check
        result.Should().EndWith("%");
        result.Should().Contain("33");
    }

    #endregion

    #region Implicit Conversion Tests

    [Fact]
    public void ImplicitConversionToDecimal_ReturnsValue()
    {
        // Arrange
        var percentage = Percentage.Create(75m);

        // Act
        decimal value = percentage;

        // Assert
        value.Should().Be(75m);
    }

    #endregion

    #region Comparison Operators Tests

    [Fact]
    public void GreaterThan_WithDecimal_ComparesCorrectly()
    {
        var percentage = Percentage.Create(75);

        (percentage > 50m).Should().BeTrue();
        (percentage > 75m).Should().BeFalse();
        (percentage > 90m).Should().BeFalse();
    }

    [Fact]
    public void LessThan_WithDecimal_ComparesCorrectly()
    {
        var percentage = Percentage.Create(25);

        (percentage < 50m).Should().BeTrue();
        (percentage < 25m).Should().BeFalse();
        (percentage < 10m).Should().BeFalse();
    }

    [Fact]
    public void GreaterThanOrEqual_WithDecimal_ComparesCorrectly()
    {
        var percentage = Percentage.Create(50);

        (percentage >= 50m).Should().BeTrue();
        (percentage >= 25m).Should().BeTrue();
        (percentage >= 75m).Should().BeFalse();
    }

    [Fact]
    public void LessThanOrEqual_WithDecimal_ComparesCorrectly()
    {
        var percentage = Percentage.Create(50);

        (percentage <= 50m).Should().BeTrue();
        (percentage <= 75m).Should().BeTrue();
        (percentage <= 25m).Should().BeFalse();
    }

    [Fact]
    public void GreaterThan_WithPercentage_ComparesCorrectly()
    {
        var high = Percentage.Create(75);
        var low = Percentage.Create(25);

        (high > low).Should().BeTrue();
        (low > high).Should().BeFalse();
    }

    [Fact]
    public void LessThan_WithPercentage_ComparesCorrectly()
    {
        var high = Percentage.Create(75);
        var low = Percentage.Create(25);

        (low < high).Should().BeTrue();
        (high < low).Should().BeFalse();
    }

    [Fact]
    public void GreaterThanOrEqual_WithPercentage_ComparesCorrectly()
    {
        var a = Percentage.Create(50);
        var b = Percentage.Create(50);
        var c = Percentage.Create(75);

        (a >= b).Should().BeTrue();
        (c >= a).Should().BeTrue();
        (a >= c).Should().BeFalse();
    }

    [Fact]
    public void LessThanOrEqual_WithPercentage_ComparesCorrectly()
    {
        var a = Percentage.Create(50);
        var b = Percentage.Create(50);
        var c = Percentage.Create(25);

        (a <= b).Should().BeTrue();
        (c <= a).Should().BeTrue();
        (a <= c).Should().BeFalse();
    }

    #endregion

    #region Value Equality Tests

    [Fact]
    public void Equals_SameValue_AreEqual()
    {
        // Arrange
        var p1 = Percentage.Create(50);
        var p2 = Percentage.Create(50);

        // Act & Assert
        p1.Should().Be(p2);
    }

    [Fact]
    public void Equals_DifferentValues_AreNotEqual()
    {
        // Arrange
        var p1 = Percentage.Create(50);
        var p2 = Percentage.Create(75);

        // Act & Assert
        p1.Should().NotBe(p2);
    }

    [Fact]
    public void GetHashCode_SameValue_ReturnsSameHashCode()
    {
        // Arrange
        var p1 = Percentage.Create(75);
        var p2 = Percentage.Create(75);

        // Act & Assert
        p1.GetHashCode().Should().Be(p2.GetHashCode());
    }

    #endregion

    #region Edge Case Tests

    [Fact]
    public void Create_WithPreciseDecimal_PreservesPrecision()
    {
        // Arrange
        var value = 33.333333333333333333333333333m;

        // Act
        var percentage = Percentage.Create(value);

        // Assert
        percentage.Value.Should().Be(value);
    }

    [Fact]
    public void RoundTrip_FromRatioToRatio_PreservesValue()
    {
        // Arrange
        var originalRatio = 0.755m;

        // Act
        var percentage = Percentage.FromRatio(originalRatio);
        var resultRatio = percentage.ToRatio();

        // Assert
        resultRatio.Should().Be(originalRatio);
    }

    #endregion
}
