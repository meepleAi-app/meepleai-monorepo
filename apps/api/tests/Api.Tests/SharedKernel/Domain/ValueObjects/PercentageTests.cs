using Api.SharedKernel.Domain.ValueObjects;
using Api.Tests.Constants;
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
        Assert.Equal(50m, percentage.Value);
    }

    [Fact]
    public void Create_MinimumValue_ReturnsPercentage()
    {
        // Act
        var percentage = Percentage.Create(0m);

        // Assert
        Assert.Equal(0m, percentage.Value);
    }

    [Fact]
    public void Create_MaximumValue_ReturnsPercentage()
    {
        // Act
        var percentage = Percentage.Create(100m);

        // Assert
        Assert.Equal(100m, percentage.Value);
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
        Assert.Equal(value, percentage.Value);
    }

    [Fact]
    public void Create_NegativeValue_ThrowsArgumentOutOfRangeException()
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => Percentage.Create(-1m));
        Assert.Equal("value", exception.ParamName);
        Assert.Contains("0 and 100", exception.Message);
    }

    [Fact]
    public void Create_OverHundredValue_ThrowsArgumentOutOfRangeException()
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => Percentage.Create(101m));
        Assert.Equal("value", exception.ParamName);
        Assert.Contains("0 and 100", exception.Message);
    }

    [Theory]
    [InlineData(-0.01)]
    [InlineData(-100)]
    [InlineData(100.01)]
    [InlineData(200)]
    public void Create_OutOfRangeValues_ThrowsArgumentOutOfRangeException(decimal value)
    {
        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => Percentage.Create(value));
    }

    #endregion

    #region FromRatio Factory Method Tests

    [Fact]
    public void FromRatio_ValidRatio_ReturnsPercentage()
    {
        // Act
        var percentage = Percentage.FromRatio(0.5m);

        // Assert
        Assert.Equal(50m, percentage.Value);
    }

    [Fact]
    public void FromRatio_ZeroRatio_ReturnsZeroPercentage()
    {
        // Act
        var percentage = Percentage.FromRatio(0m);

        // Assert
        Assert.Equal(0m, percentage.Value);
    }

    [Fact]
    public void FromRatio_OneRatio_ReturnsHundredPercentage()
    {
        // Act
        var percentage = Percentage.FromRatio(1m);

        // Assert
        Assert.Equal(100m, percentage.Value);
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
        Assert.Equal(expectedPercentage, percentage.Value);
    }

    [Fact]
    public void FromRatio_NegativeRatio_ThrowsArgumentOutOfRangeException()
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => Percentage.FromRatio(-0.1m));
        Assert.Equal("ratio", exception.ParamName);
        Assert.Contains("0.0 and 1.0", exception.Message);
    }

    [Fact]
    public void FromRatio_OverOneRatio_ThrowsArgumentOutOfRangeException()
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => Percentage.FromRatio(1.1m));
        Assert.Equal("ratio", exception.ParamName);
        Assert.Contains("0.0 and 1.0", exception.Message);
    }

    #endregion

    #region FromFraction Factory Method Tests

    [Fact]
    public void FromFraction_ValidFraction_ReturnsPercentage()
    {
        // Act
        var percentage = Percentage.FromFraction(50, 100);

        // Assert
        Assert.Equal(50m, percentage.Value);
    }

    [Fact]
    public void FromFraction_ZeroCount_ReturnsZeroPercentage()
    {
        // Act
        var percentage = Percentage.FromFraction(0, 100);

        // Assert
        Assert.Equal(0m, percentage.Value);
    }

    [Fact]
    public void FromFraction_EqualCountAndTotal_ReturnsHundredPercentage()
    {
        // Act
        var percentage = Percentage.FromFraction(100, 100);

        // Assert
        Assert.Equal(100m, percentage.Value);
    }

    [Fact]
    public void FromFraction_ZeroTotal_ReturnsZeroPercentage()
    {
        // Act
        var percentage = Percentage.FromFraction(50, 0);

        // Assert
        Assert.Equal(0m, percentage.Value);
    }

    [Fact]
    public void FromFraction_NegativeTotal_ReturnsZeroPercentage()
    {
        // Act
        var percentage = Percentage.FromFraction(50, -10);

        // Assert
        Assert.Equal(0m, percentage.Value);
    }

    [Fact]
    public void FromFraction_CountExceedsTotal_ClampsToHundred()
    {
        // Act
        var percentage = Percentage.FromFraction(150, 100);

        // Assert
        Assert.Equal(100m, percentage.Value);
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
        Assert.Equal(expectedPercentage, percentage.Value, 10); // 10 decimal precision
    }

    #endregion

    #region Static Constants Tests

    [Fact]
    public void Zero_ReturnsZeroPercentage()
    {
        // Act & Assert
        Assert.Equal(0m, Percentage.Zero.Value);
    }

    [Fact]
    public void OneHundred_ReturnsHundredPercentage()
    {
        // Act & Assert
        Assert.Equal(100m, Percentage.OneHundred.Value);
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
        Assert.Equal(expectedRatio, ratio);
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
        Assert.Equal(75m, value);
    }

    [Fact]
    public void ImplicitConversion_UsableInCalculations()
    {
        // Arrange
        var percentage = Percentage.Create(50m);

        // Act
        var result = percentage + 25m;

        // Assert
        Assert.Equal(75m, result);
    }

    #endregion

    #region Comparison Operator Tests

    [Fact]
    public void GreaterThan_Decimal_ReturnsTrue()
    {
        // Arrange
        var percentage = Percentage.Create(75m);

        // Act & Assert
        Assert.True(percentage > 50m);
    }

    [Fact]
    public void GreaterThan_Decimal_ReturnsFalse()
    {
        // Arrange
        var percentage = Percentage.Create(25m);

        // Act & Assert
        Assert.False(percentage > 50m);
    }

    [Fact]
    public void LessThan_Decimal_ReturnsTrue()
    {
        // Arrange
        var percentage = Percentage.Create(25m);

        // Act & Assert
        Assert.True(percentage < 50m);
    }

    [Fact]
    public void LessThan_Decimal_ReturnsFalse()
    {
        // Arrange
        var percentage = Percentage.Create(75m);

        // Act & Assert
        Assert.False(percentage < 50m);
    }

    [Fact]
    public void GreaterThanOrEqual_Decimal_ReturnsTrue_WhenGreater()
    {
        // Arrange
        var percentage = Percentage.Create(75m);

        // Act & Assert
        Assert.True(percentage >= 50m);
    }

    [Fact]
    public void GreaterThanOrEqual_Decimal_ReturnsTrue_WhenEqual()
    {
        // Arrange
        var percentage = Percentage.Create(50m);

        // Act & Assert
        Assert.True(percentage >= 50m);
    }

    [Fact]
    public void LessThanOrEqual_Decimal_ReturnsTrue_WhenLess()
    {
        // Arrange
        var percentage = Percentage.Create(25m);

        // Act & Assert
        Assert.True(percentage <= 50m);
    }

    [Fact]
    public void LessThanOrEqual_Decimal_ReturnsTrue_WhenEqual()
    {
        // Arrange
        var percentage = Percentage.Create(50m);

        // Act & Assert
        Assert.True(percentage <= 50m);
    }

    [Fact]
    public void GreaterThan_Percentage_ReturnsTrue()
    {
        // Arrange
        var left = Percentage.Create(75m);
        var right = Percentage.Create(50m);

        // Act & Assert
        Assert.True(left > right);
    }

    [Fact]
    public void LessThan_Percentage_ReturnsTrue()
    {
        // Arrange
        var left = Percentage.Create(25m);
        var right = Percentage.Create(50m);

        // Act & Assert
        Assert.True(left < right);
    }

    [Fact]
    public void GreaterThanOrEqual_Percentage_ReturnsTrue()
    {
        // Arrange
        var left = Percentage.Create(50m);
        var right = Percentage.Create(50m);

        // Act & Assert
        Assert.True(left >= right);
    }

    [Fact]
    public void LessThanOrEqual_Percentage_ReturnsTrue()
    {
        // Arrange
        var left = Percentage.Create(50m);
        var right = Percentage.Create(50m);

        // Act & Assert
        Assert.True(left <= right);
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
        Assert.Contains("50", result);
        Assert.EndsWith("%", result);
    }

    [Fact]
    public void ToString_WithDecimalPlaces_ReturnsFormattedString()
    {
        // Arrange
        var percentage = Percentage.Create(33.333m);

        // Act
        var result = percentage.ToString(2);

        // Assert - Culture-independent: check for percentage symbol and approximate value
        Assert.StartsWith("33", result);
        Assert.EndsWith("%", result);
    }

    [Fact]
    public void ToString_Zero_ReturnsZeroPercent()
    {
        // Act
        var result = Percentage.Zero.ToString();

        // Assert - Culture-independent: check for zero value and percentage symbol
        Assert.StartsWith("0", result);
        Assert.EndsWith("%", result);
    }

    [Fact]
    public void ToString_OneHundred_ReturnsHundredPercent()
    {
        // Act
        var result = Percentage.OneHundred.ToString();

        // Assert - Culture-independent: check for hundred value and percentage symbol
        Assert.StartsWith("100", result);
        Assert.EndsWith("%", result);
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
        Assert.Equal(percentage1, percentage2);
    }

    [Fact]
    public void Equality_DifferentValue_AreNotEqual()
    {
        // Arrange
        var percentage1 = Percentage.Create(50m);
        var percentage2 = Percentage.Create(75m);

        // Assert
        Assert.NotEqual(percentage1, percentage2);
    }

    [Fact]
    public void GetHashCode_SameValue_SameHashCode()
    {
        // Arrange
        var percentage1 = Percentage.Create(50m);
        var percentage2 = Percentage.Create(50m);

        // Assert
        Assert.Equal(percentage1.GetHashCode(), percentage2.GetHashCode());
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
        Assert.True(coverage >= minimumCoverage);
        Assert.True(passRate >= minimumPassRate);
        Assert.True(flakyRate <= maximumFlakyRate);
    }

    #endregion
}
