using Api.SharedKernel.Domain.ValueObjects;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.SharedKernel.Domain.ValueObjects;

/// <summary>
/// Unit tests for Percentage value object.
/// Issue #2382: Extracted reusable Percentage ValueObject for type-safe percentage handling.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class PercentageTests
{
    #region Create Factory Method Tests

    [Fact]
    public void Create_ValidValue_ReturnsPercentage()
    {
        // Act
        var percentage = Percentage.Create(50m);

        // Assert
        percentage.Value.Should().Be(50m);
    }

    [Fact]
    public void Create_MinimumValue_ReturnsPercentage()
    {
        // Act
        var percentage = Percentage.Create(0m);

        // Assert
        percentage.Value.Should().Be(0m);
    }

    [Fact]
    public void Create_MaximumValue_ReturnsPercentage()
    {
        // Act
        var percentage = Percentage.Create(100m);

        // Assert
        percentage.Value.Should().Be(100m);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(0.01)]
    [InlineData(25)]
    [InlineData(50)]
    [InlineData(75)]
    [InlineData(99.99)]
    [InlineData(100)]
    public void Create_ValidBoundaryValues_ReturnsPercentage(decimal value)
    {
        // Act
        var percentage = Percentage.Create(value);

        // Assert
        percentage.Value.Should().Be(value);
    }

    [Fact]
    public void Create_NegativeValue_ThrowsArgumentOutOfRangeException()
    {
        // Act & Assert
        var act = () => Percentage.Create(-1m);
        var exception = act.Should().Throw<ArgumentOutOfRangeException>().Which;
        exception.ParamName.Should().Be("value");
        exception.Message.Should().Contain("0 and 100");
    }

    [Fact]
    public void Create_OverHundredValue_ThrowsArgumentOutOfRangeException()
    {
        // Act & Assert
        var act2 = () => Percentage.Create(101m);
        var exception = act2.Should().Throw<ArgumentOutOfRangeException>().Which;
        exception.ParamName.Should().Be("value");
        exception.Message.Should().Contain("0 and 100");
    }

    [Theory]
    [InlineData(-0.01)]
    [InlineData(-100)]
    [InlineData(100.01)]
    [InlineData(200)]
    public void Create_OutOfRangeValues_ThrowsArgumentOutOfRangeException(decimal value)
    {
        // Act & Assert
        var act3 = () => Percentage.Create(value);
        act3.Should().Throw<ArgumentOutOfRangeException>();
    }

    #endregion

    #region FromRatio Factory Method Tests

    [Fact]
    public void FromRatio_ValidRatio_ReturnsPercentage()
    {
        // Act
        var percentage = Percentage.FromRatio(0.5m);

        // Assert
        percentage.Value.Should().Be(50m);
    }

    [Fact]
    public void FromRatio_ZeroRatio_ReturnsZeroPercentage()
    {
        // Act
        var percentage = Percentage.FromRatio(0m);

        // Assert
        percentage.Value.Should().Be(0m);
    }

    [Fact]
    public void FromRatio_OneRatio_ReturnsHundredPercentage()
    {
        // Act
        var percentage = Percentage.FromRatio(1m);

        // Assert
        percentage.Value.Should().Be(100m);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(0.25, 25)]
    [InlineData(0.5, 50)]
    [InlineData(0.75, 75)]
    [InlineData(1, 100)]
    public void FromRatio_VariousRatios_ReturnsCorrectPercentage(decimal ratio, decimal expectedPercentage)
    {
        // Act
        var percentage = Percentage.FromRatio(ratio);

        // Assert
        percentage.Value.Should().Be(expectedPercentage);
    }

    [Fact]
    public void FromRatio_NegativeRatio_ThrowsArgumentOutOfRangeException()
    {
        // Act & Assert
        var act4 = () => Percentage.FromRatio(-0.1m);
        var exception = act4.Should().Throw<ArgumentOutOfRangeException>().Which;
        exception.ParamName.Should().Be("ratio");
        exception.Message.Should().Contain("0.0 and 1.0");
    }

    [Fact]
    public void FromRatio_OverOneRatio_ThrowsArgumentOutOfRangeException()
    {
        // Act & Assert
        var act5 = () => Percentage.FromRatio(1.1m);
        var exception = act5.Should().Throw<ArgumentOutOfRangeException>().Which;
        exception.ParamName.Should().Be("ratio");
        exception.Message.Should().Contain("0.0 and 1.0");
    }

    #endregion

    #region FromFraction Factory Method Tests

    [Fact]
    public void FromFraction_ValidFraction_ReturnsPercentage()
    {
        // Act
        var percentage = Percentage.FromFraction(50, 100);

        // Assert
        percentage.Value.Should().Be(50m);
    }

    [Fact]
    public void FromFraction_ZeroCount_ReturnsZeroPercentage()
    {
        // Act
        var percentage = Percentage.FromFraction(0, 100);

        // Assert
        percentage.Value.Should().Be(0m);
    }

    [Fact]
    public void FromFraction_EqualCountAndTotal_ReturnsHundredPercentage()
    {
        // Act
        var percentage = Percentage.FromFraction(100, 100);

        // Assert
        percentage.Value.Should().Be(100m);
    }

    [Fact]
    public void FromFraction_ZeroTotal_ReturnsZeroPercentage()
    {
        // Act
        var percentage = Percentage.FromFraction(50, 0);

        // Assert
        percentage.Value.Should().Be(0m);
    }

    [Fact]
    public void FromFraction_NegativeTotal_ReturnsZeroPercentage()
    {
        // Act
        var percentage = Percentage.FromFraction(50, -10);

        // Assert
        percentage.Value.Should().Be(0m);
    }

    [Fact]
    public void FromFraction_CountExceedsTotal_ClampsToHundred()
    {
        // Act
        var percentage = Percentage.FromFraction(150, 100);

        // Assert
        percentage.Value.Should().Be(100m);
    }

    [Theory]
    [InlineData(0, 100, 0)]
    [InlineData(25, 100, 25)]
    [InlineData(50, 100, 50)]
    [InlineData(75, 100, 75)]
    [InlineData(100, 100, 100)]
    [InlineData(1, 3, 33.333333333333333333333333333)] // 1/3 as decimal
    public void FromFraction_VariousFractions_ReturnsCorrectPercentage(int count, int total, decimal expectedPercentage)
    {
        // Act
        var percentage = Percentage.FromFraction(count, total);

        // Assert
        percentage.Value.Should().Be(expectedPercentage);
    }

    #endregion

    #region Static Constants Tests

    [Fact]
    public void Zero_ReturnsZeroPercentage()
    {
        // Act & Assert
        Percentage.Zero.Value.Should().Be(0m);
    }

    [Fact]
    public void OneHundred_ReturnsHundredPercentage()
    {
        // Act & Assert
        Percentage.OneHundred.Value.Should().Be(100m);
    }

    #endregion

    #region ToRatio Tests

    [Theory]
    [InlineData(0, 0)]
    [InlineData(25, 0.25)]
    [InlineData(50, 0.5)]
    [InlineData(75, 0.75)]
    [InlineData(100, 1)]
    public void ToRatio_ReturnsCorrectRatio(decimal percentageValue, decimal expectedRatio)
    {
        // Arrange
        var percentage = Percentage.Create(percentageValue);

        // Act
        var ratio = percentage.ToRatio();

        // Assert
        ratio.Should().Be(expectedRatio);
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

    [Fact]
    public void ImplicitConversion_UsableInCalculations()
    {
        // Arrange
        var percentage = Percentage.Create(50m);

        // Act
        var result = percentage + 25m;

        // Assert
        result.Should().Be(75m);
    }

    #endregion

    #region Comparison Operator Tests

    [Fact]
    public void GreaterThan_Decimal_ReturnsTrue()
    {
        // Arrange
        var percentage = Percentage.Create(75m);

        // Act & Assert
        (percentage > 50m).Should().BeTrue();
    }

    [Fact]
    public void GreaterThan_Decimal_ReturnsFalse()
    {
        // Arrange
        var percentage = Percentage.Create(25m);

        // Act & Assert
        (percentage > 50m).Should().BeFalse();
    }

    [Fact]
    public void LessThan_Decimal_ReturnsTrue()
    {
        // Arrange
        var percentage = Percentage.Create(25m);

        // Act & Assert
        (percentage < 50m).Should().BeTrue();
    }

    [Fact]
    public void LessThan_Decimal_ReturnsFalse()
    {
        // Arrange
        var percentage = Percentage.Create(75m);

        // Act & Assert
        (percentage < 50m).Should().BeFalse();
    }

    [Fact]
    public void GreaterThanOrEqual_Decimal_ReturnsTrue_WhenGreater()
    {
        // Arrange
        var percentage = Percentage.Create(75m);

        // Act & Assert
        (percentage >= 50m).Should().BeTrue();
    }

    [Fact]
    public void GreaterThanOrEqual_Decimal_ReturnsTrue_WhenEqual()
    {
        // Arrange
        var percentage = Percentage.Create(50m);

        // Act & Assert
        (percentage >= 50m).Should().BeTrue();
    }

    [Fact]
    public void LessThanOrEqual_Decimal_ReturnsTrue_WhenLess()
    {
        // Arrange
        var percentage = Percentage.Create(25m);

        // Act & Assert
        (percentage <= 50m).Should().BeTrue();
    }

    [Fact]
    public void LessThanOrEqual_Decimal_ReturnsTrue_WhenEqual()
    {
        // Arrange
        var percentage = Percentage.Create(50m);

        // Act & Assert
        (percentage <= 50m).Should().BeTrue();
    }

    [Fact]
    public void GreaterThan_Percentage_ReturnsTrue()
    {
        // Arrange
        var left = Percentage.Create(75m);
        var right = Percentage.Create(50m);

        // Act & Assert
        (left > right).Should().BeTrue();
    }

    [Fact]
    public void LessThan_Percentage_ReturnsTrue()
    {
        // Arrange
        var left = Percentage.Create(25m);
        var right = Percentage.Create(50m);

        // Act & Assert
        (left < right).Should().BeTrue();
    }

    [Fact]
    public void GreaterThanOrEqual_Percentage_ReturnsTrue()
    {
        // Arrange
        var left = Percentage.Create(50m);
        var right = Percentage.Create(50m);

        // Act & Assert
        (left >= right).Should().BeTrue();
    }

    [Fact]
    public void LessThanOrEqual_Percentage_ReturnsTrue()
    {
        // Arrange
        var left = Percentage.Create(50m);
        var right = Percentage.Create(50m);

        // Act & Assert
        (left <= right).Should().BeTrue();
    }

    #endregion

    #region ToString Tests

    [Fact]
    public void ToString_ReturnsFormattedString()
    {
        // Arrange
        var percentage = Percentage.Create(50m);

        // Act
        var result = percentage.ToString();

        // Assert - Culture-independent: check for value and percentage symbol
        result.Should().Contain("50");
        result.Should().EndWith("%");
    }

    [Fact]
    public void ToString_WithDecimalPlaces_ReturnsFormattedString()
    {
        // Arrange
        var percentage = Percentage.Create(33.333m);

        // Act
        var result = percentage.ToString(2);

        // Assert - Culture-independent: check for percentage symbol and approximate value
        result.Should().StartWith("33");
        result.Should().EndWith("%");
    }

    [Fact]
    public void ToString_Zero_ReturnsZeroPercent()
    {
        // Act
        var result = Percentage.Zero.ToString();

        // Assert - Culture-independent: check for zero value and percentage symbol
        result.Should().StartWith("0");
        result.Should().EndWith("%");
    }

    [Fact]
    public void ToString_OneHundred_ReturnsHundredPercent()
    {
        // Act
        var result = Percentage.OneHundred.ToString();

        // Assert - Culture-independent: check for hundred value and percentage symbol
        result.Should().StartWith("100");
        result.Should().EndWith("%");
    }

    #endregion

    #region Equality Tests

    [Fact]
    public void Equality_SameValue_AreEqual()
    {
        // Arrange
        var percentage1 = Percentage.Create(50m);
        var percentage2 = Percentage.Create(50m);

        // Assert
        percentage2.Should().Be(percentage1);
    }

    [Fact]
    public void Equality_DifferentValue_AreNotEqual()
    {
        // Arrange
        var percentage1 = Percentage.Create(50m);
        var percentage2 = Percentage.Create(75m);

        // Assert
        percentage2.Should().NotBe(percentage1);
    }

    [Fact]
    public void GetHashCode_SameValue_SameHashCode()
    {
        // Arrange
        var percentage1 = Percentage.Create(50m);
        var percentage2 = Percentage.Create(50m);

        // Assert
        percentage2.GetHashCode().Should().Be(percentage1.GetHashCode());
    }

    #endregion

    #region Integration with Quality Thresholds

    [Fact]
    public void Percentage_UsableWithQualityThresholdComparisons()
    {
        // Arrange - simulating threshold comparisons like in E2EMetrics
        var coverage = Percentage.Create(95m);
        var passRate = Percentage.Create(98m);
        var flakyRate = Percentage.Create(3m);

        const decimal minimumCoverage = 90m;
        const decimal minimumPassRate = 95m;
        const decimal maximumFlakyRate = 5m;

        // Act & Assert - demonstrating usage in quality standard checks
        (coverage >= minimumCoverage).Should().BeTrue();
        (passRate >= minimumPassRate).Should().BeTrue();
        (flakyRate <= maximumFlakyRate).Should().BeTrue();
    }

    #endregion
}
