using Api.BoundedContexts.GameManagement.Domain.ValueObjects;
using Api.SharedKernel.Domain.Exceptions;
using Xunit;
using Api.Tests.Constants;

namespace Api.Tests.BoundedContexts.GameManagement.Domain;

[Trait("Category", TestCategories.Unit)]

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

    [Theory]
    [InlineData(15, 25, true, false, false)]    // Quick game
    [InlineData(45, 75, false, true, false)]    // Medium game
    [InlineData(120, 180, false, false, true)]  // Long game
    public void PlayTimeCategory_ReturnsCorrectClassification(int minMinutes, int maxMinutes, bool expectedIsQuick, bool expectedIsMedium, bool expectedIsLong)
    {
        // Arrange
        var playTime = new PlayTime(minMinutes, maxMinutes);

        // Act & Assert
        Assert.Equal(expectedIsQuick, playTime.IsQuick);
        Assert.Equal(expectedIsMedium, playTime.IsMedium);
        Assert.Equal(expectedIsLong, playTime.IsLong);
    }

    [Theory]
    [InlineData("Quick", 15, 30, true, false, false)]
    [InlineData("Standard", 45, 60, false, true, false)]
    [InlineData("Long", 120, 180, false, false, true)]
    public void FactoryMethod_CreatesCorrectPlayTime(string methodName, int expectedMin, int expectedMax, bool expectedIsQuick, bool expectedIsMedium, bool expectedIsLong)
    {
        // Arrange & Act
        var playTime = methodName switch
        {
            "Quick" => PlayTime.Quick,
            "Standard" => PlayTime.Standard,
            "Long" => PlayTime.Long,
            _ => throw new ArgumentException($"Unknown factory method: {methodName}")
        };

        // Assert
        Assert.Equal(expectedMin, playTime.MinMinutes);
        Assert.Equal(expectedMax, playTime.MaxMinutes);
        Assert.Equal(expectedIsQuick, playTime.IsQuick);
        Assert.Equal(expectedIsMedium, playTime.IsMedium);
        Assert.Equal(expectedIsLong, playTime.IsLong);
    }

    [Fact]
    public void PlayTime_BelowMinimum_ThrowsValidationException()
    {
        // Act & Assert
        var exception = Assert.Throws<ValidationException>(() => new PlayTime(0, 60));
        Assert.Contains("must be between", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void PlayTime_AboveMaximum_ThrowsValidationException()
    {
        // Act & Assert
        var exception = Assert.Throws<ValidationException>(() => new PlayTime(30, 1500));
        Assert.Contains("must be between", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void PlayTime_MinGreaterThanMax_ThrowsValidationException()
    {
        // Act & Assert
        var exception = Assert.Throws<ValidationException>(() => new PlayTime(90, 60));
        Assert.Contains("cannot exceed", exception.Message, StringComparison.OrdinalIgnoreCase);
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
