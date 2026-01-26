using Api.BoundedContexts.GameManagement.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.GameManagement.Domain.ValueObjects;

/// <summary>
/// Tests for the GamePhase enum.
/// Issue #3025: Backend 90% Coverage Target - Phase 3
/// </summary>
[Trait("Category", "Unit")]
public sealed class GamePhaseTests
{
    #region Enum Value Tests

    [Fact]
    public void GamePhase_Setup_HasCorrectValue()
    {
        // Assert
        ((int)GamePhase.Setup).Should().Be(0);
    }

    [Fact]
    public void GamePhase_InProgress_HasCorrectValue()
    {
        // Assert
        ((int)GamePhase.InProgress).Should().Be(1);
    }

    [Fact]
    public void GamePhase_Scoring_HasCorrectValue()
    {
        // Assert
        ((int)GamePhase.Scoring).Should().Be(2);
    }

    [Fact]
    public void GamePhase_Completed_HasCorrectValue()
    {
        // Assert
        ((int)GamePhase.Completed).Should().Be(3);
    }

    [Fact]
    public void GamePhase_Custom_HasCorrectValue()
    {
        // Assert
        ((int)GamePhase.Custom).Should().Be(99);
    }

    #endregion

    #region Enum Completeness Tests

    [Fact]
    public void GamePhase_HasFiveValues()
    {
        // Arrange
        var values = Enum.GetValues<GamePhase>();

        // Assert
        values.Should().HaveCount(5);
    }

    [Fact]
    public void GamePhase_AllValuesCanBeParsed()
    {
        // Arrange
        var names = new[] { "Setup", "InProgress", "Scoring", "Completed", "Custom" };

        // Act & Assert
        foreach (var name in names)
        {
            var parsed = Enum.Parse<GamePhase>(name);
            parsed.Should().BeOneOf(Enum.GetValues<GamePhase>());
        }
    }

    [Fact]
    public void GamePhase_ToString_ReturnsExpectedNames()
    {
        // Assert
        GamePhase.Setup.ToString().Should().Be("Setup");
        GamePhase.InProgress.ToString().Should().Be("InProgress");
        GamePhase.Scoring.ToString().Should().Be("Scoring");
        GamePhase.Completed.ToString().Should().Be("Completed");
        GamePhase.Custom.ToString().Should().Be("Custom");
    }

    #endregion

    #region Conversion Tests

    [Fact]
    public void GamePhase_CastFromInt_ReturnsCorrectPhases()
    {
        // Assert
        ((GamePhase)0).Should().Be(GamePhase.Setup);
        ((GamePhase)1).Should().Be(GamePhase.InProgress);
        ((GamePhase)2).Should().Be(GamePhase.Scoring);
        ((GamePhase)3).Should().Be(GamePhase.Completed);
        ((GamePhase)99).Should().Be(GamePhase.Custom);
    }

    [Fact]
    public void GamePhase_IsDefined_ReturnsTrueForValidValues()
    {
        // Assert
        Enum.IsDefined(typeof(GamePhase), 0).Should().BeTrue();
        Enum.IsDefined(typeof(GamePhase), 1).Should().BeTrue();
        Enum.IsDefined(typeof(GamePhase), 2).Should().BeTrue();
        Enum.IsDefined(typeof(GamePhase), 3).Should().BeTrue();
        Enum.IsDefined(typeof(GamePhase), 99).Should().BeTrue();
    }

    [Fact]
    public void GamePhase_IsDefined_ReturnsFalseForInvalidValues()
    {
        // Assert
        Enum.IsDefined(typeof(GamePhase), 4).Should().BeFalse();
        Enum.IsDefined(typeof(GamePhase), -1).Should().BeFalse();
        Enum.IsDefined(typeof(GamePhase), 100).Should().BeFalse();
    }

    #endregion
}
