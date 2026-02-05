using Api.BoundedContexts.SessionTracking.Domain.Entities;
using Api.Tests.Constants;
using Xunit;

namespace Api.Tests.BoundedContexts.SessionTracking.Domain;

/// <summary>
/// Unit tests for DiceFormulaParser.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SessionTracking")]
public class DiceFormulaParserTests
{
    [Theory]
    [InlineData("d4", 1, 4, 0)]
    [InlineData("d6", 1, 6, 0)]
    [InlineData("d8", 1, 8, 0)]
    [InlineData("d10", 1, 10, 0)]
    [InlineData("d12", 1, 12, 0)]
    [InlineData("d20", 1, 20, 0)]
    [InlineData("d100", 1, 100, 0)]
    [InlineData("D20", 1, 20, 0)]
    [InlineData("D100", 1, 100, 0)]
    public void Parse_SingleDice_NoModifier_ReturnsCorrectValues(string formula, int expectedCount, int expectedSides, int expectedModifier)
    {
        // Act
        var (count, sides, modifier) = DiceFormulaParser.Parse(formula);

        // Assert
        Assert.Equal(expectedCount, count);
        Assert.Equal(expectedSides, sides);
        Assert.Equal(expectedModifier, modifier);
    }

    [Theory]
    [InlineData("2d6", 2, 6, 0)]
    [InlineData("3d8", 3, 8, 0)]
    [InlineData("4d10", 4, 10, 0)]
    [InlineData("10d6", 10, 6, 0)]
    [InlineData("100d20", 100, 20, 0)]
    public void Parse_MultipleDice_NoModifier_ReturnsCorrectValues(string formula, int expectedCount, int expectedSides, int expectedModifier)
    {
        // Act
        var (count, sides, modifier) = DiceFormulaParser.Parse(formula);

        // Assert
        Assert.Equal(expectedCount, count);
        Assert.Equal(expectedSides, sides);
        Assert.Equal(expectedModifier, modifier);
    }

    [Theory]
    [InlineData("1d20+5", 1, 20, 5)]
    [InlineData("2d6+3", 2, 6, 3)]
    [InlineData("d8+10", 1, 8, 10)]
    [InlineData("3d6+100", 3, 6, 100)]
    public void Parse_WithPositiveModifier_ReturnsCorrectValues(string formula, int expectedCount, int expectedSides, int expectedModifier)
    {
        // Act
        var (count, sides, modifier) = DiceFormulaParser.Parse(formula);

        // Assert
        Assert.Equal(expectedCount, count);
        Assert.Equal(expectedSides, sides);
        Assert.Equal(expectedModifier, modifier);
    }

    [Theory]
    [InlineData("1d20-2", 1, 20, -2)]
    [InlineData("2d6-1", 2, 6, -1)]
    [InlineData("d8-5", 1, 8, -5)]
    [InlineData("3d6-100", 3, 6, -100)]
    public void Parse_WithNegativeModifier_ReturnsCorrectValues(string formula, int expectedCount, int expectedSides, int expectedModifier)
    {
        // Act
        var (count, sides, modifier) = DiceFormulaParser.Parse(formula);

        // Assert
        Assert.Equal(expectedCount, count);
        Assert.Equal(expectedSides, sides);
        Assert.Equal(expectedModifier, modifier);
    }

    [Theory]
    [InlineData(" 2d6 + 3 ")] // Spaces are removed
    [InlineData("2D6+3")]     // Uppercase is normalized
    [InlineData("2d6+3")]     // Standard format
    public void Parse_NormalizesInput_ReturnsCorrectValues(string formula)
    {
        // Act
        var (count, sides, modifier) = DiceFormulaParser.Parse(formula);

        // Assert
        Assert.Equal(2, count);
        Assert.Equal(6, sides);
        Assert.Equal(3, modifier);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public void Parse_EmptyOrNull_ThrowsArgumentException(string? formula)
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => DiceFormulaParser.Parse(formula!));
    }

    [Theory]
    [InlineData("invalid")]
    [InlineData("roll")]
    [InlineData("2+3")]
    [InlineData("abc123")]
    [InlineData("d")]
    [InlineData("2d")]
    [InlineData("dd6")]
    [InlineData("2dd6")]
    public void Parse_InvalidFormat_ThrowsArgumentException(string formula)
    {
        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() => DiceFormulaParser.Parse(formula));
        Assert.Contains("Invalid dice formula", ex.Message);
    }

    [Theory]
    [InlineData("d3")]   // d3 not supported
    [InlineData("d5")]   // d5 not supported
    [InlineData("d7")]   // d7 not supported
    [InlineData("d9")]   // d9 not supported
    [InlineData("d11")]  // d11 not supported
    [InlineData("d13")]  // d13 not supported
    [InlineData("d50")]  // d50 not supported
    public void Parse_UnsupportedDiceType_ThrowsArgumentException(string formula)
    {
        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() => DiceFormulaParser.Parse(formula));
        Assert.Contains("Unsupported dice type", ex.Message);
    }

    [Theory]
    [InlineData("0d6")]    // Zero dice
    [InlineData("-1d6")]   // Negative dice (won't match regex)
    [InlineData("101d6")]  // Too many dice
    public void Parse_InvalidDiceCount_ThrowsArgumentException(string formula)
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => DiceFormulaParser.Parse(formula));
    }

    [Theory]
    [InlineData("d6+101")]  // Modifier too large
    [InlineData("d6-101")]  // Modifier too small
    public void Parse_InvalidModifier_ThrowsArgumentException(string formula)
    {
        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() => DiceFormulaParser.Parse(formula));
        Assert.Contains("Modifier must be between", ex.Message);
    }

    [Fact]
    public void SupportedSides_ContainsAllStandardDice()
    {
        // Assert
        Assert.Contains(4, DiceFormulaParser.SupportedSides);
        Assert.Contains(6, DiceFormulaParser.SupportedSides);
        Assert.Contains(8, DiceFormulaParser.SupportedSides);
        Assert.Contains(10, DiceFormulaParser.SupportedSides);
        Assert.Contains(12, DiceFormulaParser.SupportedSides);
        Assert.Contains(20, DiceFormulaParser.SupportedSides);
        Assert.Contains(100, DiceFormulaParser.SupportedSides);
        Assert.Equal(7, DiceFormulaParser.SupportedSides.Length);
    }
}
