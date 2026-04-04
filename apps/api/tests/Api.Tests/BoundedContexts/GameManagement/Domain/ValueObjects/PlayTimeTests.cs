using Api.BoundedContexts.GameManagement.Domain.ValueObjects;
using Api.SharedKernel.Constants;
using Api.SharedKernel.Domain.Exceptions;
using Api.Tests.Constants;
using Xunit;
using FluentAssertions;

namespace Api.Tests.BoundedContexts.GameManagement.Domain.ValueObjects;

/// <summary>
/// Unit tests for PlayTime value object.
/// Issue #2381: Comprehensive validation logic testing for game play time ranges.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class PlayTimeTests
{
    #region Constructor Tests

    [Fact]
    public void Constructor_ValidRange_CreatesInstance()
    {
        // Act
        var playTime = new PlayTime(30, 60);

        // Assert
        playTime.MinMinutes.Should().Be(30);
        playTime.MaxMinutes.Should().Be(60);
    }

    [Fact]
    public void Constructor_SameMinMax_CreatesInstance()
    {
        // Act - MinMinutes == MaxMinutes is valid
        var playTime = new PlayTime(45, 45);

        // Assert
        playTime.MinMinutes.Should().Be(45);
        playTime.MaxMinutes.Should().Be(45);
    }

    [Fact]
    public void Constructor_MinimumValues_CreatesInstance()
    {
        // Act - 1 minute is minimum valid
        var playTime = new PlayTime(1, 1);

        // Assert
        playTime.MinMinutes.Should().Be(1);
        playTime.MaxMinutes.Should().Be(1);
    }

    [Fact]
    public void Constructor_MaximumValues_CreatesInstance()
    {
        // Act - 1440 minutes (24 hours) is maximum valid
        var playTime = new PlayTime(1440, 1440);

        // Assert
        playTime.MinMinutes.Should().Be(1440);
        playTime.MaxMinutes.Should().Be(1440);
    }

    [Fact]
    public void Constructor_ZeroMinMinutes_ThrowsValidationException()
    {
        // Act & Assert
        var exception = ((Action)(() => new PlayTime(0, 60))).Should().Throw<ValidationException>().Which;
        exception.Message.Should().Contain("minMinutes");
    }

    [Fact]
    public void Constructor_NegativeMinMinutes_ThrowsValidationException()
    {
        // Act & Assert
        var exception = ((Action)(() => new PlayTime(-1, 60))).Should().Throw<ValidationException>().Which;
        exception.Message.Should().Contain("minMinutes");
    }

    [Fact]
    public void Constructor_ZeroMaxMinutes_ThrowsValidationException()
    {
        // Act & Assert
        var exception = ((Action)(() => new PlayTime(30, 0))).Should().Throw<ValidationException>().Which;
        exception.Message.Should().Contain("maxMinutes");
    }

    [Fact]
    public void Constructor_NegativeMaxMinutes_ThrowsValidationException()
    {
        // Act & Assert
        var exception = ((Action)(() => new PlayTime(30, -1))).Should().Throw<ValidationException>().Which;
        exception.Message.Should().Contain("maxMinutes");
    }

    [Fact]
    public void Constructor_MinMinutesExceedsMax_ThrowsValidationException()
    {
        // Act & Assert - MaxMinutes > 1440 (24 hours)
        var exception = ((Action)(() => new PlayTime(30, 1441))).Should().Throw<ValidationException>().Which;
        exception.Message.Should().Contain("maxMinutes");
    }

    [Fact]
    public void Constructor_MinMinutesGreaterThanMaxMinutes_ThrowsValidationException()
    {
        // Act & Assert - Invalid range: min > max
        var exception = ((Action)(() => new PlayTime(60, 30))).Should().Throw<ValidationException>().Which;
        exception.Message.ToLower().Should().Contain("minminutes");
    }

    #endregion

    #region AverageMinutes Tests

    [Fact]
    public void AverageMinutes_CalculatesCorrectly()
    {
        // Arrange
        var playTime = new PlayTime(30, 60);

        // Assert
        playTime.AverageMinutes.Should().Be(45); // (30 + 60) / 2
    }

    [Fact]
    public void AverageMinutes_SameMinMax_ReturnsValue()
    {
        // Arrange
        var playTime = new PlayTime(45, 45);

        // Assert
        playTime.AverageMinutes.Should().Be(45);
    }

    [Fact]
    public void AverageMinutes_OddSum_TruncatesCorrectly()
    {
        // Arrange - (15 + 30) / 2 = 22.5 -> 22 (integer division)
        var playTime = new PlayTime(15, 30);

        // Assert
        playTime.AverageMinutes.Should().Be(22);
    }

    #endregion

    #region Time Category Tests

    [Fact]
    public void IsQuick_MaxUnderThirtyMinutes_ReturnsTrue()
    {
        // Arrange
        var playTime = new PlayTime(10, 25);

        // Assert
        (playTime.IsQuick).Should().BeTrue();
    }

    [Fact]
    public void IsQuick_MaxExactlyThirtyMinutes_ReturnsTrue()
    {
        // Arrange
        var playTime = new PlayTime(15, 30);

        // Assert
        (playTime.IsQuick).Should().BeTrue();
    }

    [Fact]
    public void IsQuick_MaxAboveThirtyMinutes_ReturnsFalse()
    {
        // Arrange
        var playTime = new PlayTime(15, 31);

        // Assert
        (playTime.IsQuick).Should().BeFalse();
    }

    [Fact]
    public void IsMedium_BetweenThirtyAndNinetyMinutes_ReturnsTrue()
    {
        // Arrange - MinMinutes >= 30 AND MaxMinutes <= 90
        var playTime = new PlayTime(45, 60);

        // Assert
        (playTime.IsMedium).Should().BeTrue();
    }

    [Fact]
    public void IsMedium_ExactlyAtThresholds_ReturnsTrue()
    {
        // Arrange - MinMinutes == 30 AND MaxMinutes == 90
        var playTime = new PlayTime(30, 90);

        // Assert
        (playTime.IsMedium).Should().BeTrue();
    }

    [Fact]
    public void IsMedium_MinBelowThirty_ReturnsFalse()
    {
        // Arrange
        var playTime = new PlayTime(25, 60);

        // Assert
        (playTime.IsMedium).Should().BeFalse();
    }

    [Fact]
    public void IsMedium_MaxAboveNinety_ReturnsFalse()
    {
        // Arrange
        var playTime = new PlayTime(30, 100);

        // Assert
        (playTime.IsMedium).Should().BeFalse();
    }

    [Fact]
    public void IsLong_MinAboveNinetyMinutes_ReturnsTrue()
    {
        // Arrange
        var playTime = new PlayTime(120, 180);

        // Assert
        (playTime.IsLong).Should().BeTrue();
    }

    [Fact]
    public void IsLong_MinExactlyNinety_ReturnsFalse()
    {
        // Arrange - MinMinutes must be > 90, not ==
        var playTime = new PlayTime(90, 120);

        // Assert
        (playTime.IsLong).Should().BeFalse();
    }

    [Fact]
    public void IsLong_MinNinetyOne_ReturnsTrue()
    {
        // Arrange
        var playTime = new PlayTime(91, 120);

        // Assert
        (playTime.IsLong).Should().BeTrue();
    }

    #endregion

    #region Static Factory Properties Tests

    [Fact]
    public void Quick_CreatesCorrectRange()
    {
        // Act
        var quick = PlayTime.Quick;

        // Assert
        quick.MinMinutes.Should().Be(15);
        quick.MaxMinutes.Should().Be(30);
        (quick.IsQuick).Should().BeTrue();
    }

    [Fact]
    public void Standard_CreatesCorrectRange()
    {
        // Act
        var standard = PlayTime.Standard;

        // Assert
        standard.MinMinutes.Should().Be(45);
        standard.MaxMinutes.Should().Be(60);
        (standard.IsMedium).Should().BeTrue();
    }

    [Fact]
    public void Long_CreatesCorrectRange()
    {
        // Act
        var longGame = PlayTime.Long;

        // Assert
        longGame.MinMinutes.Should().Be(120);
        longGame.MaxMinutes.Should().Be(180);
        (longGame.IsLong).Should().BeTrue();
    }

    #endregion

    #region ToString Tests

    [Fact]
    public void ToString_DifferentMinMax_FormatsAsRange()
    {
        // Arrange
        var playTime = new PlayTime(30, 60);

        // Act & Assert
        playTime.ToString().Should().Be("30-60 min");
    }

    [Fact]
    public void ToString_SameMinMax_FormatsSingleValue()
    {
        // Arrange
        var playTime = new PlayTime(45, 45);

        // Act & Assert
        playTime.ToString().Should().Be("45 min");
    }

    #endregion

    #region Equality Tests

    [Fact]
    public void Equality_SameValues_AreEqual()
    {
        // Arrange
        var playTime1 = new PlayTime(30, 60);
        var playTime2 = new PlayTime(30, 60);

        // Assert
        playTime2.Should().Be(playTime1);
        (playTime1 == playTime2).Should().BeTrue();
        (playTime1 != playTime2).Should().BeFalse();
    }

    [Fact]
    public void Equality_DifferentMinMinutes_AreNotEqual()
    {
        // Arrange
        var playTime1 = new PlayTime(30, 60);
        var playTime2 = new PlayTime(45, 60);

        // Assert
        playTime2.Should().NotBe(playTime1);
    }

    [Fact]
    public void Equality_DifferentMaxMinutes_AreNotEqual()
    {
        // Arrange
        var playTime1 = new PlayTime(30, 60);
        var playTime2 = new PlayTime(30, 90);

        // Assert
        playTime2.Should().NotBe(playTime1);
    }

    #endregion

    #region Edge Cases

    [Theory]
    [InlineData(1, 1)]
    [InlineData(1, 30)]
    [InlineData(30, 30)]
    [InlineData(30, 90)]
    [InlineData(91, 180)]
    [InlineData(1440, 1440)]
    public void Constructor_BoundaryValues_CreatesInstance(int min, int max)
    {
        // Act
        var playTime = new PlayTime(min, max);

        // Assert
        playTime.MinMinutes.Should().Be(min);
        playTime.MaxMinutes.Should().Be(max);
    }

    #endregion
}
