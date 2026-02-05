using Api.BoundedContexts.SessionTracking.Domain.Entities;
using Api.Tests.Constants;
using Xunit;

namespace Api.Tests.BoundedContexts.SessionTracking.Domain;

/// <summary>
/// Unit tests for DiceRoll domain entity.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SessionTracking")]
public class DiceRollTests
{
    private readonly Guid _sessionId = Guid.NewGuid();
    private readonly Guid _participantId = Guid.NewGuid();

    [Fact]
    public void Create_ValidFormula_ReturnsDiceRoll()
    {
        // Act
        var diceRoll = DiceRoll.Create(_sessionId, _participantId, "2d6+3");

        // Assert
        Assert.NotEqual(Guid.Empty, diceRoll.Id);
        Assert.Equal(_sessionId, diceRoll.SessionId);
        Assert.Equal(_participantId, diceRoll.ParticipantId);
        Assert.Equal("2D6+3", diceRoll.Formula);
        Assert.Null(diceRoll.Label);
        Assert.Equal(3, diceRoll.Modifier);
        Assert.Equal(2, diceRoll.GetRolls().Length);
        Assert.All(diceRoll.GetRolls(), r => Assert.InRange(r, 1, 6));
        Assert.False(diceRoll.IsDeleted);
    }

    [Fact]
    public void Create_WithLabel_SetsLabel()
    {
        // Act
        var diceRoll = DiceRoll.Create(_sessionId, _participantId, "1d20", "Attack roll");

        // Assert
        Assert.Equal("Attack roll", diceRoll.Label);
    }

    [Fact]
    public void Create_SimpleFormula_CalculatesTotalCorrectly()
    {
        // Act
        var diceRoll = DiceRoll.Create(_sessionId, _participantId, "1d6");

        // Assert
        var rolls = diceRoll.GetRolls();
        Assert.Single(rolls);
        Assert.Equal(rolls[0], diceRoll.Total); // No modifier
    }

    [Fact]
    public void Create_FormulaWithPositiveModifier_CalculatesTotalCorrectly()
    {
        // Act
        var diceRoll = DiceRoll.Create(_sessionId, _participantId, "1d6+5");

        // Assert
        var rolls = diceRoll.GetRolls();
        Assert.Single(rolls);
        Assert.Equal(rolls[0] + 5, diceRoll.Total);
    }

    [Fact]
    public void Create_FormulaWithNegativeModifier_CalculatesTotalCorrectly()
    {
        // Act
        var diceRoll = DiceRoll.Create(_sessionId, _participantId, "1d6-2");

        // Assert
        var rolls = diceRoll.GetRolls();
        Assert.Single(rolls);
        Assert.Equal(rolls[0] - 2, diceRoll.Total);
    }

    [Fact]
    public void Create_EmptySessionId_ThrowsArgumentException()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() =>
            DiceRoll.Create(Guid.Empty, _participantId, "1d20"));
    }

    [Fact]
    public void Create_EmptyParticipantId_ThrowsArgumentException()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() =>
            DiceRoll.Create(_sessionId, Guid.Empty, "1d20"));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public void Create_EmptyFormula_ThrowsArgumentException(string? formula)
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() =>
            DiceRoll.Create(_sessionId, _participantId, formula!));
    }

    [Fact]
    public void Create_InvalidFormula_ThrowsArgumentException()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() =>
            DiceRoll.Create(_sessionId, _participantId, "invalid"));
    }

    [Fact]
    public void Create_MultipleDice_RollsCorrectCount()
    {
        // Act
        var diceRoll = DiceRoll.Create(_sessionId, _participantId, "5d6");

        // Assert
        var rolls = diceRoll.GetRolls();
        Assert.Equal(5, rolls.Length);
        Assert.All(rolls, r => Assert.InRange(r, 1, 6));
    }

    [Fact]
    public void Create_D20_RollsInRange()
    {
        // Act
        var diceRoll = DiceRoll.Create(_sessionId, _participantId, "1d20");

        // Assert
        var rolls = diceRoll.GetRolls();
        Assert.Single(rolls);
        Assert.InRange(rolls[0], 1, 20);
    }

    [Fact]
    public void Create_D100_RollsInRange()
    {
        // Act
        var diceRoll = DiceRoll.Create(_sessionId, _participantId, "1d100");

        // Assert
        var rolls = diceRoll.GetRolls();
        Assert.Single(rolls);
        Assert.InRange(rolls[0], 1, 100);
    }

    [Fact]
    public void Create_SetsTimestamp()
    {
        // Arrange
        var before = DateTime.UtcNow;

        // Act
        var diceRoll = DiceRoll.Create(_sessionId, _participantId, "1d20");

        // Assert
        var after = DateTime.UtcNow;
        Assert.InRange(diceRoll.Timestamp, before, after);
    }

    [Fact]
    public void Create_FormulaNormalizedToUppercase()
    {
        // Act
        var diceRoll = DiceRoll.Create(_sessionId, _participantId, "2d6+3");

        // Assert
        Assert.Equal("2D6+3", diceRoll.Formula);
    }

    [Fact]
    public void GetRolls_ReturnsCorrectArray()
    {
        // Act
        var diceRoll = DiceRoll.Create(_sessionId, _participantId, "3d6");

        // Assert
        var rolls = diceRoll.GetRolls();
        Assert.Equal(3, rolls.Length);
    }

    [Fact]
    public void SoftDelete_SetsFlags()
    {
        // Arrange
        var diceRoll = DiceRoll.Create(_sessionId, _participantId, "1d20");

        // Act
        diceRoll.SoftDelete();

        // Assert
        Assert.True(diceRoll.IsDeleted);
        Assert.NotNull(diceRoll.DeletedAt);
    }

    [Fact]
    public void Create_TotalIsConsistent_MultipleCalls()
    {
        // Run multiple times to test RNG consistency
        for (int i = 0; i < 100; i++)
        {
            // Act
            var diceRoll = DiceRoll.Create(_sessionId, _participantId, "2d6+3");

            // Assert
            var rolls = diceRoll.GetRolls();
            Assert.Equal(rolls.Sum() + 3, diceRoll.Total);
        }
    }
}
