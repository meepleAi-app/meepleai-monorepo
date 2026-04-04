using Api.BoundedContexts.SessionTracking.Domain.Entities;
using Api.Tests.Constants;
using FluentAssertions;
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
        diceRoll.Id.Should().NotBe(Guid.Empty);
        diceRoll.SessionId.Should().Be(_sessionId);
        diceRoll.ParticipantId.Should().Be(_participantId);
        diceRoll.Formula.Should().Be("2D6+3");
        diceRoll.Label.Should().BeNull();
        diceRoll.Modifier.Should().Be(3);
        diceRoll.GetRolls().Length.Should().Be(2);
        diceRoll.GetRolls().Should().OnlyContain(r => r >= 1 && r <= 6);
        diceRoll.IsDeleted.Should().BeFalse();
    }

    [Fact]
    public void Create_WithLabel_SetsLabel()
    {
        // Act
        var diceRoll = DiceRoll.Create(_sessionId, _participantId, "1d20", "Attack roll");

        // Assert
        diceRoll.Label.Should().Be("Attack roll");
    }

    [Fact]
    public void Create_SimpleFormula_CalculatesTotalCorrectly()
    {
        // Act
        var diceRoll = DiceRoll.Create(_sessionId, _participantId, "1d6");

        // Assert
        var rolls = diceRoll.GetRolls();
        rolls.Should().ContainSingle();
        diceRoll.Total.Should().Be(rolls[0]);
    }

    [Fact]
    public void Create_FormulaWithPositiveModifier_CalculatesTotalCorrectly()
    {
        // Act
        var diceRoll = DiceRoll.Create(_sessionId, _participantId, "1d6+5");

        // Assert
        var rolls = diceRoll.GetRolls();
        rolls.Should().ContainSingle();
        diceRoll.Total.Should().Be(rolls[0] + 5);
    }

    [Fact]
    public void Create_FormulaWithNegativeModifier_CalculatesTotalCorrectly()
    {
        // Act
        var diceRoll = DiceRoll.Create(_sessionId, _participantId, "1d6-2");

        // Assert
        var rolls = diceRoll.GetRolls();
        rolls.Should().ContainSingle();
        diceRoll.Total.Should().Be(rolls[0] - 2);
    }

    [Fact]
    public void Create_EmptySessionId_ThrowsArgumentException()
    {
        // Act & Assert
        var act = () =>
            DiceRoll.Create(Guid.Empty, _participantId, "1d20");
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_EmptyParticipantId_ThrowsArgumentException()
    {
        // Act & Assert
        var act2 = () =>
            DiceRoll.Create(_sessionId, Guid.Empty, "1d20");
        act2.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public void Create_EmptyFormula_ThrowsArgumentException(string? formula)
    {
        // Act & Assert
        var act3 = () =>
            DiceRoll.Create(_sessionId, _participantId, formula!);
        act3.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_InvalidFormula_ThrowsArgumentException()
    {
        // Act & Assert
        var act4 = () =>
            DiceRoll.Create(_sessionId, _participantId, "invalid");
        act4.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_MultipleDice_RollsCorrectCount()
    {
        // Act
        var diceRoll = DiceRoll.Create(_sessionId, _participantId, "5d6");

        // Assert
        var rolls = diceRoll.GetRolls();
        rolls.Length.Should().Be(5);
        rolls.Should().OnlyContain(r => r >= 1 && r <= 6);
    }

    [Fact]
    public void Create_D20_RollsInRange()
    {
        // Act
        var diceRoll = DiceRoll.Create(_sessionId, _participantId, "1d20");

        // Assert
        var rolls = diceRoll.GetRolls();
        rolls.Should().ContainSingle();
        rolls[0].Should().BeInRange(1, 20);
    }

    [Fact]
    public void Create_D100_RollsInRange()
    {
        // Act
        var diceRoll = DiceRoll.Create(_sessionId, _participantId, "1d100");

        // Assert
        var rolls = diceRoll.GetRolls();
        rolls.Should().ContainSingle();
        rolls[0].Should().BeInRange(1, 100);
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
        diceRoll.Timestamp.Should().BeOnOrAfter(before).And.BeOnOrBefore(after);
    }

    [Fact]
    public void Create_FormulaNormalizedToUppercase()
    {
        // Act
        var diceRoll = DiceRoll.Create(_sessionId, _participantId, "2d6+3");

        // Assert
        diceRoll.Formula.Should().Be("2D6+3");
    }

    [Fact]
    public void GetRolls_ReturnsCorrectArray()
    {
        // Act
        var diceRoll = DiceRoll.Create(_sessionId, _participantId, "3d6");

        // Assert
        var rolls = diceRoll.GetRolls();
        rolls.Length.Should().Be(3);
    }

    [Fact]
    public void SoftDelete_SetsFlags()
    {
        // Arrange
        var diceRoll = DiceRoll.Create(_sessionId, _participantId, "1d20");

        // Act
        diceRoll.SoftDelete();

        // Assert
        diceRoll.IsDeleted.Should().BeTrue();
        diceRoll.DeletedAt.Should().NotBeNull();
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
            diceRoll.Total.Should().Be(rolls.Sum() + 3);
        }
    }
}
