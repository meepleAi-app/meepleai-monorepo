using Api.BoundedContexts.GameManagement.Domain.ValueObjects;
using Api.SharedKernel.Domain.Exceptions;
using Xunit;
using Api.Tests.Constants;

namespace Api.Tests.BoundedContexts.GameManagement.Domain;

[Trait("Category", TestCategories.Unit)]

public class PlayerCountTests
{
    [Fact]
    public void PlayerCount_WithValidRange_CreatesSuccessfully()
    {
        // Arrange & Act
        var playerCount = new PlayerCount(2, 4);

        // Assert
        Assert.Equal(2, playerCount.Min);
        Assert.Equal(4, playerCount.Max);
    }

    [Fact]
    public void PlayerCount_Supports_ReturnsCorrectly()
    {
        // Arrange
        var playerCount = new PlayerCount(2, 4);

        // Act & Assert
        Assert.True(playerCount.Supports(2));
        Assert.True(playerCount.Supports(3));
        Assert.True(playerCount.Supports(4));
        Assert.False(playerCount.Supports(1));
        Assert.False(playerCount.Supports(5));
    }

    [Fact]
    public void PlayerCount_SupportsSolo_WhenMinIsOne()
    {
        // Arrange
        var soloGame = new PlayerCount(1, 4);
        var multiplayerGame = new PlayerCount(2, 4);

        // Act & Assert
        Assert.True(soloGame.SupportsSolo);
        Assert.False(multiplayerGame.SupportsSolo);
    }

    [Fact]
    public void PlayerCount_Solo_FactoryMethod_Works()
    {
        // Arrange & Act
        var solo = PlayerCount.Solo;

        // Assert
        Assert.Equal(1, solo.Min);
        Assert.Equal(1, solo.Max);
        Assert.True(solo.SupportsSolo);
    }

    [Fact]
    public void PlayerCount_Standard_FactoryMethod_Works()
    {
        // Arrange & Act
        var standard = PlayerCount.Standard;

        // Assert
        Assert.Equal(2, standard.Min);
        Assert.Equal(4, standard.Max);
    }

    [Fact]
    public void PlayerCount_BelowMinimum_ThrowsValidationException()
    {
        // Act & Assert
        var exception = Assert.Throws<ValidationException>(() => new PlayerCount(0, 4));
        Assert.Contains("cannot be less than 1", exception.Message);
    }

    [Fact]
    public void PlayerCount_AboveMaximum_ThrowsValidationException()
    {
        // Act & Assert
        var exception = Assert.Throws<ValidationException>(() => new PlayerCount(1, 101));
        Assert.Contains("cannot exceed 100", exception.Message);
    }

    [Fact]
    public void PlayerCount_MinGreaterThanMax_ThrowsValidationException()
    {
        // Act & Assert
        var exception = Assert.Throws<ValidationException>(() => new PlayerCount(5, 2));
        Assert.Contains("cannot exceed maximum", exception.Message);
    }

    [Fact]
    public void PlayerCount_ToString_FormatsCorrectly()
    {
        // Arrange
        var range = new PlayerCount(2, 4);
        var single = new PlayerCount(3, 3);

        // Act & Assert
        Assert.Equal("2-4", range.ToString());
        Assert.Equal("3", single.ToString());
    }

    [Fact]
    public void PlayerCount_EqualityComparison_WorksCorrectly()
    {
        // Arrange
        var count1 = new PlayerCount(2, 4);
        var count2 = new PlayerCount(2, 4);
        var count3 = new PlayerCount(3, 5);

        // Act & Assert
        Assert.Equal(count1, count2);
        Assert.NotEqual(count1, count3);
    }
}

