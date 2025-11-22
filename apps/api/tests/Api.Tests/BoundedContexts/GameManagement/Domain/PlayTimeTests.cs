using Api.BoundedContexts.GameManagement.Domain.ValueObjects;
using Api.SharedKernel.Domain.Exceptions;
using Xunit;

namespace Api.Tests.BoundedContexts.GameManagement.Domain;

public class PlayTimeTests
{
    [Fact]
    public void PlayTime_WithValidRange_CreatesSuccessfully()
    {
        // Arrange & Act
        var playTime = new PlayTime(30, 60);

        // Assert
        Assert.Equal(30, playTime.MinMinutes);
        Assert.Equal(60, playTime.MaxMinutes);
    }

    [Fact]
    public void PlayTime_AverageMinutes_CalculatesCorrectly()
    {
        // Arrange
        var playTime = new PlayTime(30, 90);

        // Act
        var average = playTime.AverageMinutes;

        // Assert
        Assert.Equal(60, average);
    }

    [Fact]
    public void PlayTime_IsQuick_WhenMaxBelow30Minutes()
    {
        // Arrange
        var quick = new PlayTime(15, 25);
        var notQuick = new PlayTime(30, 60);

        // Act & Assert
        Assert.True(quick.IsQuick);
        Assert.False(notQuick.IsQuick);
    }

    [Fact]
    public void PlayTime_IsMedium_WhenBetween30And90Minutes()
    {
        // Arrange
        var medium = new PlayTime(45, 75);
        var shortGame = new PlayTime(15, 25);
        var longGame = new PlayTime(120, 180);

        // Act & Assert
        Assert.True(medium.IsMedium);
        Assert.False(shortGame.IsMedium);
        Assert.False(longGame.IsMedium);
    }

    [Fact]
    public void PlayTime_IsLong_WhenMinAbove90Minutes()
    {
        // Arrange
        var longGame = new PlayTime(120, 180);
        var medium = new PlayTime(60, 90);

        // Act & Assert
        Assert.True(longGame.IsLong);
        Assert.False(medium.IsLong);
    }

    [Fact]
    public void PlayTime_Quick_FactoryMethod_Works()
    {
        // Arrange & Act
        var quick = PlayTime.Quick;

        // Assert
        Assert.Equal(15, quick.MinMinutes);
        Assert.Equal(30, quick.MaxMinutes);
        Assert.True(quick.IsQuick);
    }

    [Fact]
    public void PlayTime_Standard_FactoryMethod_Works()
    {
        // Arrange & Act
        var standard = PlayTime.Standard;

        // Assert
        Assert.Equal(45, standard.MinMinutes);
        Assert.Equal(60, standard.MaxMinutes);
    }

    [Fact]
    public void PlayTime_Long_FactoryMethod_Works()
    {
        // Arrange & Act
        var longGame = PlayTime.Long;

        // Assert
        Assert.Equal(120, longGame.MinMinutes);
        Assert.Equal(180, longGame.MaxMinutes);
        Assert.True(longGame.IsLong);
    }

    [Fact]
    public void PlayTime_BelowMinimum_ThrowsValidationException()
    {
        // Act & Assert
        var exception = Assert.Throws<ValidationException>(() => new PlayTime(0, 60));
        Assert.Contains("cannot be less than 1 minute", exception.Message);
    }

    [Fact]
    public void PlayTime_AboveMaximum_ThrowsValidationException()
    {
        // Act & Assert
        var exception = Assert.Throws<ValidationException>(() => new PlayTime(30, 1500));
        Assert.Contains("cannot exceed 1440 minutes", exception.Message);
    }

    [Fact]
    public void PlayTime_MinGreaterThanMax_ThrowsValidationException()
    {
        // Act & Assert
        var exception = Assert.Throws<ValidationException>(() => new PlayTime(90, 60));
        Assert.Contains("cannot exceed maximum", exception.Message);
    }

    [Fact]
    public void PlayTime_ToString_FormatsCorrectly()
    {
        // Arrange
        var range = new PlayTime(30, 60);
        var exact = new PlayTime(45, 45);

        // Act & Assert
        Assert.Equal("30-60 min", range.ToString());
        Assert.Equal("45 min", exact.ToString());
    }

    [Fact]
    public void PlayTime_EqualityComparison_WorksCorrectly()
    {
        // Arrange
        var time1 = new PlayTime(30, 60);
        var time2 = new PlayTime(30, 60);
        var time3 = new PlayTime(45, 90);

        // Act & Assert
        Assert.Equal(time1, time2);
        Assert.NotEqual(time1, time3);
    }
}
