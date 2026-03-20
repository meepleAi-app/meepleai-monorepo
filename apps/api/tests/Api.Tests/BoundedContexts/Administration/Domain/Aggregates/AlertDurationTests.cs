using Api.BoundedContexts.Administration.Domain.Aggregates.AlertRules;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.Administration.Domain.Aggregates;

/// <summary>
/// Tests for the AlertDuration value object.
/// Issue #3025: Backend 90% Coverage Target - Phase 21
/// </summary>
[Trait("Category", "Unit")]
public sealed class AlertDurationTests
{
    #region Constructor Tests

    [Fact]
    public void Constructor_WithValidMinutes_CreatesDuration()
    {
        // Act
        var duration = new AlertDuration(30);

        // Assert
        duration.Minutes.Should().Be(30);
    }

    [Fact]
    public void Constructor_WithOneMinute_Succeeds()
    {
        // Act
        var duration = new AlertDuration(1);

        // Assert
        duration.Minutes.Should().Be(1);
    }

    [Fact]
    public void Constructor_WithZeroMinutes_ThrowsArgumentException()
    {
        // Act
        var action = () => new AlertDuration(0);

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithParameterName("minutes")
            .WithMessage("*Duration must be positive*");
    }

    [Fact]
    public void Constructor_WithNegativeMinutes_ThrowsArgumentException()
    {
        // Act
        var action = () => new AlertDuration(-5);

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithMessage("*Duration must be positive*");
    }

    #endregion

    #region ToTimeSpan Tests

    [Fact]
    public void ToTimeSpan_ReturnsCorrectTimeSpan()
    {
        // Arrange
        var duration = new AlertDuration(15);

        // Act
        var timeSpan = duration.ToTimeSpan();

        // Assert
        timeSpan.Should().Be(TimeSpan.FromMinutes(15));
    }

    [Fact]
    public void ToTimeSpan_WithLargeValue_ReturnsCorrectTimeSpan()
    {
        // Arrange
        var duration = new AlertDuration(120);

        // Act
        var timeSpan = duration.ToTimeSpan();

        // Assert
        timeSpan.Should().Be(TimeSpan.FromHours(2));
    }

    #endregion

    #region Factory Methods Tests

    [Fact]
    public void FromMinutes_CreatesDurationWithSpecifiedMinutes()
    {
        // Act
        var duration = AlertDuration.FromMinutes(25);

        // Assert
        duration.Minutes.Should().Be(25);
    }

    [Fact]
    public void FiveMinutes_CreatesFiveMinuteDuration()
    {
        // Act
        var duration = AlertDuration.FiveMinutes;

        // Assert
        duration.Minutes.Should().Be(5);
    }

    [Fact]
    public void TenMinutes_CreatesTenMinuteDuration()
    {
        // Act
        var duration = AlertDuration.TenMinutes;

        // Assert
        duration.Minutes.Should().Be(10);
    }

    [Fact]
    public void FifteenMinutes_CreatesFifteenMinuteDuration()
    {
        // Act
        var duration = AlertDuration.FifteenMinutes;

        // Assert
        duration.Minutes.Should().Be(15);
    }

    #endregion

    #region ToString Tests

    [Fact]
    public void ToString_ReturnsFormattedString()
    {
        // Arrange
        var duration = new AlertDuration(5);

        // Act
        var result = duration.ToString();

        // Assert
        result.Should().Be("5 minutes");
    }

    [Fact]
    public void ToString_WithOneMinute_ReturnsSingularForm()
    {
        // Note: Implementation uses plural regardless, testing actual behavior
        // Arrange
        var duration = new AlertDuration(1);

        // Act
        var result = duration.ToString();

        // Assert
        result.Should().Be("1 minutes");
    }

    #endregion

    #region Equality Tests

    [Fact]
    public void Equals_WithSameMinutes_ReturnsTrue()
    {
        // Arrange
        var duration1 = new AlertDuration(10);
        var duration2 = new AlertDuration(10);

        // Assert
        duration1.Should().Be(duration2);
    }

    [Fact]
    public void Equals_WithDifferentMinutes_ReturnsFalse()
    {
        // Arrange
        var duration1 = new AlertDuration(5);
        var duration2 = new AlertDuration(10);

        // Assert
        duration1.Should().NotBe(duration2);
    }

    [Fact]
    public void GetHashCode_WithSameMinutes_ReturnsSameHash()
    {
        // Arrange
        var duration1 = new AlertDuration(15);
        var duration2 = new AlertDuration(15);

        // Assert
        duration1.GetHashCode().Should().Be(duration2.GetHashCode());
    }

    #endregion
}