using Api.BoundedContexts.GameManagement.Domain.ValueObjects;
using Api.SharedKernel.Constants;
using Api.SharedKernel.Domain.Exceptions;
using Api.Tests.Constants;
using Xunit;

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
        Assert.Equal(30, playTime.MinMinutes);
        Assert.Equal(60, playTime.MaxMinutes);
    }

    [Fact]
    public void Constructor_SameMinMax_CreatesInstance()
    {
        // Act - MinMinutes == MaxMinutes is valid
        var playTime = new PlayTime(45, 45);

        // Assert
        Assert.Equal(45, playTime.MinMinutes);
        Assert.Equal(45, playTime.MaxMinutes);
    }

    [Fact]
    public void Constructor_MinimumValues_CreatesInstance()
    {
        // Act - 1 minute is minimum valid
        var playTime = new PlayTime(1, 1);

        // Assert
        Assert.Equal(1, playTime.MinMinutes);
        Assert.Equal(1, playTime.MaxMinutes);
    }

    [Fact]
    public void Constructor_MaximumValues_CreatesInstance()
    {
        // Act - 1440 minutes (24 hours) is maximum valid
        var playTime = new PlayTime(1440, 1440);

        // Assert
        Assert.Equal(1440, playTime.MinMinutes);
        Assert.Equal(1440, playTime.MaxMinutes);
    }

    [Fact]
    public void Constructor_ZeroMinMinutes_ThrowsValidationException()
    {
        // Act & Assert
        var exception = Assert.Throws<ValidationException>(() => new PlayTime(0, 60));
        Assert.Contains("minMinutes", exception.Message);
    }

    [Fact]
    public void Constructor_NegativeMinMinutes_ThrowsValidationException()
    {
        // Act & Assert
        var exception = Assert.Throws<ValidationException>(() => new PlayTime(-1, 60));
        Assert.Contains("minMinutes", exception.Message);
    }

    [Fact]
    public void Constructor_ZeroMaxMinutes_ThrowsValidationException()
    {
        // Act & Assert
        var exception = Assert.Throws<ValidationException>(() => new PlayTime(30, 0));
        Assert.Contains("maxMinutes", exception.Message);
    }

    [Fact]
    public void Constructor_NegativeMaxMinutes_ThrowsValidationException()
    {
        // Act & Assert
        var exception = Assert.Throws<ValidationException>(() => new PlayTime(30, -1));
        Assert.Contains("maxMinutes", exception.Message);
    }

    [Fact]
    public void Constructor_MinMinutesExceedsMax_ThrowsValidationException()
    {
        // Act & Assert - MaxMinutes > 1440 (24 hours)
        var exception = Assert.Throws<ValidationException>(() => new PlayTime(30, 1441));
        Assert.Contains("maxMinutes", exception.Message);
    }

    [Fact]
    public void Constructor_MinMinutesGreaterThanMaxMinutes_ThrowsValidationException()
    {
        // Act & Assert - Invalid range: min > max
        var exception = Assert.Throws<ValidationException>(() => new PlayTime(60, 30));
        Assert.Contains("minminutes", exception.Message.ToLower());
    }

    #endregion

    #region AverageMinutes Tests

    [Fact]
    public void AverageMinutes_CalculatesCorrectly()
    {
        // Arrange
        var playTime = new PlayTime(30, 60);

        // Assert
        Assert.Equal(45, playTime.AverageMinutes); // (30 + 60) / 2
    }

    [Fact]
    public void AverageMinutes_SameMinMax_ReturnsValue()
    {
        // Arrange
        var playTime = new PlayTime(45, 45);

        // Assert
        Assert.Equal(45, playTime.AverageMinutes);
    }

    [Fact]
    public void AverageMinutes_OddSum_TruncatesCorrectly()
    {
        // Arrange - (15 + 30) / 2 = 22.5 -> 22 (integer division)
        var playTime = new PlayTime(15, 30);

        // Assert
        Assert.Equal(22, playTime.AverageMinutes);
    }

    #endregion

    #region Time Category Tests

    [Fact]
    public void IsQuick_MaxUnderThirtyMinutes_ReturnsTrue()
    {
        // Arrange
        var playTime = new PlayTime(10, 25);

        // Assert
        Assert.True(playTime.IsQuick);
    }

    [Fact]
    public void IsQuick_MaxExactlyThirtyMinutes_ReturnsTrue()
    {
        // Arrange
        var playTime = new PlayTime(15, 30);

        // Assert
        Assert.True(playTime.IsQuick);
    }

    [Fact]
    public void IsQuick_MaxAboveThirtyMinutes_ReturnsFalse()
    {
        // Arrange
        var playTime = new PlayTime(15, 31);

        // Assert
        Assert.False(playTime.IsQuick);
    }

    [Fact]
    public void IsMedium_BetweenThirtyAndNinetyMinutes_ReturnsTrue()
    {
        // Arrange - MinMinutes >= 30 AND MaxMinutes <= 90
        var playTime = new PlayTime(45, 60);

        // Assert
        Assert.True(playTime.IsMedium);
    }

    [Fact]
    public void IsMedium_ExactlyAtThresholds_ReturnsTrue()
    {
        // Arrange - MinMinutes == 30 AND MaxMinutes == 90
        var playTime = new PlayTime(30, 90);

        // Assert
        Assert.True(playTime.IsMedium);
    }

    [Fact]
    public void IsMedium_MinBelowThirty_ReturnsFalse()
    {
        // Arrange
        var playTime = new PlayTime(25, 60);

        // Assert
        Assert.False(playTime.IsMedium);
    }

    [Fact]
    public void IsMedium_MaxAboveNinety_ReturnsFalse()
    {
        // Arrange
        var playTime = new PlayTime(30, 100);

        // Assert
        Assert.False(playTime.IsMedium);
    }

    [Fact]
    public void IsLong_MinAboveNinetyMinutes_ReturnsTrue()
    {
        // Arrange
        var playTime = new PlayTime(120, 180);

        // Assert
        Assert.True(playTime.IsLong);
    }

    [Fact]
    public void IsLong_MinExactlyNinety_ReturnsFalse()
    {
        // Arrange - MinMinutes must be > 90, not ==
        var playTime = new PlayTime(90, 120);

        // Assert
        Assert.False(playTime.IsLong);
    }

    [Fact]
    public void IsLong_MinNinetyOne_ReturnsTrue()
    {
        // Arrange
        var playTime = new PlayTime(91, 120);

        // Assert
        Assert.True(playTime.IsLong);
    }

    #endregion

    #region Static Factory Properties Tests

    [Fact]
    public void Quick_CreatesCorrectRange()
    {
        // Act
        var quick = PlayTime.Quick;

        // Assert
        Assert.Equal(15, quick.MinMinutes);
        Assert.Equal(30, quick.MaxMinutes);
        Assert.True(quick.IsQuick);
    }

    [Fact]
    public void Standard_CreatesCorrectRange()
    {
        // Act
        var standard = PlayTime.Standard;

        // Assert
        Assert.Equal(45, standard.MinMinutes);
        Assert.Equal(60, standard.MaxMinutes);
        Assert.True(standard.IsMedium);
    }

    [Fact]
    public void Long_CreatesCorrectRange()
    {
        // Act
        var longGame = PlayTime.Long;

        // Assert
        Assert.Equal(120, longGame.MinMinutes);
        Assert.Equal(180, longGame.MaxMinutes);
        Assert.True(longGame.IsLong);
    }

    #endregion

    #region ToString Tests

    [Fact]
    public void ToString_DifferentMinMax_FormatsAsRange()
    {
        // Arrange
        var playTime = new PlayTime(30, 60);

        // Act & Assert
        Assert.Equal("30-60 min", playTime.ToString());
    }

    [Fact]
    public void ToString_SameMinMax_FormatsSingleValue()
    {
        // Arrange
        var playTime = new PlayTime(45, 45);

        // Act & Assert
        Assert.Equal("45 min", playTime.ToString());
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
        Assert.Equal(playTime1, playTime2);
        Assert.True(playTime1 == playTime2);
        Assert.False(playTime1 != playTime2);
    }

    [Fact]
    public void Equality_DifferentMinMinutes_AreNotEqual()
    {
        // Arrange
        var playTime1 = new PlayTime(30, 60);
        var playTime2 = new PlayTime(45, 60);

        // Assert
        Assert.NotEqual(playTime1, playTime2);
    }

    [Fact]
    public void Equality_DifferentMaxMinutes_AreNotEqual()
    {
        // Arrange
        var playTime1 = new PlayTime(30, 60);
        var playTime2 = new PlayTime(30, 90);

        // Assert
        Assert.NotEqual(playTime1, playTime2);
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
        Assert.Equal(min, playTime.MinMinutes);
        Assert.Equal(max, playTime.MaxMinutes);
    }

    #endregion
}
