using Api.BoundedContexts.Administration.Domain.Aggregates.AlertRules;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.Administration.Domain.Aggregates;

/// <summary>
/// Tests for the AlertThreshold value object.
/// Issue #3025: Backend 90% Coverage Target - Phase 21
/// </summary>
[Trait("Category", "Unit")]
public sealed class AlertThresholdTests
{
    #region Constructor Tests

    [Fact]
    public void Constructor_WithValidParameters_CreatesThreshold()
    {
        // Act
        var threshold = new AlertThreshold(80.5, "percent");

        // Assert
        threshold.Value.Should().Be(80.5);
        threshold.Unit.Should().Be("percent");
    }

    [Fact]
    public void Constructor_WithZeroValue_Succeeds()
    {
        // Act
        var threshold = new AlertThreshold(0, "count");

        // Assert
        threshold.Value.Should().Be(0);
    }

    [Fact]
    public void Constructor_WithNegativeValue_ThrowsArgumentException()
    {
        // Act
        var action = () => new AlertThreshold(-1, "percent");

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithParameterName("value")
            .WithMessage("*Threshold value cannot be negative*");
    }

    [Fact]
    public void Constructor_WithEmptyUnit_ThrowsArgumentException()
    {
        // Act
        var action = () => new AlertThreshold(80, "");

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithParameterName("unit")
            .WithMessage("*Unit cannot be empty*");
    }

    [Fact]
    public void Constructor_WithWhitespaceUnit_ThrowsArgumentException()
    {
        // Act
        var action = () => new AlertThreshold(80, "   ");

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithMessage("*Unit cannot be empty*");
    }

    #endregion

    #region IsExceeded Tests

    [Fact]
    public void IsExceeded_WhenMetricExceedsThreshold_ReturnsTrue()
    {
        // Arrange
        var threshold = new AlertThreshold(80, "%");

        // Act
        var result = threshold.IsExceeded(85);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void IsExceeded_WhenMetricEqualsThreshold_ReturnsTrue()
    {
        // Arrange
        var threshold = new AlertThreshold(80, "%");

        // Act
        var result = threshold.IsExceeded(80);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void IsExceeded_WhenMetricBelowThreshold_ReturnsFalse()
    {
        // Arrange
        var threshold = new AlertThreshold(80, "%");

        // Act
        var result = threshold.IsExceeded(79.9);

        // Assert
        result.Should().BeFalse();
    }

    #endregion

    #region Factory Methods Tests

    [Fact]
    public void Percentage_CreatesThresholdWithPercentUnit()
    {
        // Act
        var threshold = AlertThreshold.Percentage(95);

        // Assert
        threshold.Value.Should().Be(95);
        threshold.Unit.Should().Be("%");
    }

    [Fact]
    public void Milliseconds_CreatesThresholdWithMsUnit()
    {
        // Act
        var threshold = AlertThreshold.Milliseconds(500);

        // Assert
        threshold.Value.Should().Be(500);
        threshold.Unit.Should().Be("ms");
    }

    [Fact]
    public void Count_CreatesThresholdWithCountUnit()
    {
        // Act
        var threshold = AlertThreshold.Count(100);

        // Assert
        threshold.Value.Should().Be(100);
        threshold.Unit.Should().Be("count");
    }

    #endregion

    #region ToString Tests

    [Fact]
    public void ToString_ReturnsFormattedString()
    {
        // Arrange
        var threshold = new AlertThreshold(80, "%");

        // Act
        var result = threshold.ToString();

        // Assert
        result.Should().Be("80 %");
    }

    [Fact]
    public void ToString_WithDecimalValue_FormatsCorrectly()
    {
        // Arrange
        var threshold = new AlertThreshold(99.5, "percent");

        // Act
        var result = threshold.ToString();

        // Assert - Culture-independent: accept either "." or "," as decimal separator
        result.Should().MatchRegex(@"99[.,]5 percent");
    }

    #endregion

    #region Equality Tests

    [Fact]
    public void Equals_WithSameValues_ReturnsTrue()
    {
        // Arrange
        var threshold1 = new AlertThreshold(80, "%");
        var threshold2 = new AlertThreshold(80, "%");

        // Assert
        threshold1.Should().Be(threshold2);
    }

    [Fact]
    public void Equals_WithDifferentValue_ReturnsFalse()
    {
        // Arrange
        var threshold1 = new AlertThreshold(80, "%");
        var threshold2 = new AlertThreshold(90, "%");

        // Assert
        threshold1.Should().NotBe(threshold2);
    }

    [Fact]
    public void Equals_WithDifferentUnit_ReturnsFalse()
    {
        // Arrange
        var threshold1 = new AlertThreshold(80, "%");
        var threshold2 = new AlertThreshold(80, "percent");

        // Assert
        threshold1.Should().NotBe(threshold2);
    }

    #endregion
}
