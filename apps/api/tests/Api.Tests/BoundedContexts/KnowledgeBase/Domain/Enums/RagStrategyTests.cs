using Api.BoundedContexts.KnowledgeBase.Domain.Enums;
using Api.Tests.Constants;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Domain.Enums;

/// <summary>
/// Unit tests for RagStrategy enum and extensions.
/// Issue #3436: Part of tier-strategy-model architecture.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
public class RagStrategyTests
{
    #region GetDisplayName Tests

    [Theory]
    [InlineData(RagStrategy.Fast, "FAST")]
    [InlineData(RagStrategy.Balanced, "BALANCED")]
    [InlineData(RagStrategy.Precise, "PRECISE")]
    [InlineData(RagStrategy.Expert, "EXPERT")]
    [InlineData(RagStrategy.Consensus, "CONSENSUS")]
    [InlineData(RagStrategy.Custom, "CUSTOM")]
    public void GetDisplayName_AllStrategies_ReturnsUpperCaseName(RagStrategy strategy, string expectedName)
    {
        // Act
        var displayName = strategy.GetDisplayName();

        // Assert
        Assert.Equal(expectedName, displayName);
    }

    #endregion

    #region Parse Tests

    [Theory]
    [InlineData("FAST", RagStrategy.Fast)]
    [InlineData("fast", RagStrategy.Fast)]
    [InlineData("Fast", RagStrategy.Fast)]
    [InlineData("BALANCED", RagStrategy.Balanced)]
    [InlineData("balanced", RagStrategy.Balanced)]
    [InlineData("PRECISE", RagStrategy.Precise)]
    [InlineData("precise", RagStrategy.Precise)]
    [InlineData("EXPERT", RagStrategy.Expert)]
    [InlineData("CONSENSUS", RagStrategy.Consensus)]
    [InlineData("CUSTOM", RagStrategy.Custom)]
    public void Parse_ValidStrings_ReturnsCorrectStrategy(string input, RagStrategy expected)
    {
        // Act
        var result = RagStrategyExtensions.Parse(input);

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("INVALID")]
    [InlineData("UNKNOWN")]
    [InlineData("SPEEDY")]
    public void Parse_InvalidStrings_ThrowsArgumentException(string input)
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() => RagStrategyExtensions.Parse(input));
        Assert.Contains("Invalid RAG strategy", exception.Message);
    }

    #endregion

    #region TryParse Tests

    [Theory]
    [InlineData("FAST", true, RagStrategy.Fast)]
    [InlineData("fast", true, RagStrategy.Fast)]
    [InlineData("BALANCED", true, RagStrategy.Balanced)]
    [InlineData("balanced", true, RagStrategy.Balanced)]
    [InlineData("PRECISE", true, RagStrategy.Precise)]
    [InlineData("precise", true, RagStrategy.Precise)]
    [InlineData("EXPERT", true, RagStrategy.Expert)]
    [InlineData("CONSENSUS", true, RagStrategy.Consensus)]
    [InlineData("CUSTOM", true, RagStrategy.Custom)]
    [InlineData("INVALID", false, RagStrategy.Fast)]
    [InlineData("", false, RagStrategy.Fast)]
    [InlineData(null, false, RagStrategy.Fast)]
    public void TryParse_VariousInputs_ReturnsExpectedResult(
        string? input, bool expectedSuccess, RagStrategy expectedStrategy)
    {
        // Act
        var success = RagStrategyExtensions.TryParse(input, out var result);

        // Assert
        Assert.Equal(expectedSuccess, success);
        if (expectedSuccess)
        {
            Assert.Equal(expectedStrategy, result);
        }
    }

    [Fact]
    public void TryParse_WhitespaceInput_ReturnsFalse()
    {
        // Act
        var success = RagStrategyExtensions.TryParse("   ", out _);

        // Assert
        Assert.False(success);
    }

    [Fact]
    public void TryParse_TrimmedInput_Works()
    {
        // Act
        var success = RagStrategyExtensions.TryParse("  FAST  ", out var result);

        // Assert
        Assert.True(success);
        Assert.Equal(RagStrategy.Fast, result);
    }

    #endregion

    #region GetComplexityLevel Tests

    [Theory]
    [InlineData(RagStrategy.Fast, 0)]
    [InlineData(RagStrategy.Balanced, 1)]
    [InlineData(RagStrategy.Precise, 2)]
    [InlineData(RagStrategy.Expert, 3)]
    [InlineData(RagStrategy.Consensus, 4)]
    [InlineData(RagStrategy.Custom, 5)]
    public void GetComplexityLevel_AllStrategies_ReturnsCorrectLevel(RagStrategy strategy, int expectedLevel)
    {
        // Act
        var level = strategy.GetComplexityLevel();

        // Assert
        Assert.Equal(expectedLevel, level);
    }

    [Fact]
    public void GetComplexityLevel_StrategiesOrderedByComplexity()
    {
        // Arrange
        var strategies = Enum.GetValues<RagStrategy>().OrderBy(s => s.GetComplexityLevel()).ToList();

        // Assert
        Assert.Equal(RagStrategy.Fast, strategies[0]);
        Assert.Equal(RagStrategy.Balanced, strategies[1]);
        Assert.Equal(RagStrategy.Precise, strategies[2]);
        Assert.Equal(RagStrategy.Expert, strategies[3]);
        Assert.Equal(RagStrategy.Consensus, strategies[4]);
        Assert.Equal(RagStrategy.Custom, strategies[5]);
    }

    #endregion

    #region RequiresAdmin Tests

    [Theory]
    [InlineData(RagStrategy.Fast, false)]
    [InlineData(RagStrategy.Balanced, false)]
    [InlineData(RagStrategy.Precise, false)]
    [InlineData(RagStrategy.Expert, false)]
    [InlineData(RagStrategy.Consensus, false)]
    [InlineData(RagStrategy.Custom, true)]
    public void RequiresAdmin_AllStrategies_OnlyCustomRequiresAdmin(RagStrategy strategy, bool expectedRequiresAdmin)
    {
        // Act
        var requiresAdmin = strategy.RequiresAdmin();

        // Assert
        Assert.Equal(expectedRequiresAdmin, requiresAdmin);
    }

    #endregion

    #region Enum Completeness Tests

    [Fact]
    public void RagStrategy_HasExpectedNumberOfValues()
    {
        // Assert
        var values = Enum.GetValues<RagStrategy>();
        Assert.Equal(6, values.Length);
    }

    [Fact]
    public void RagStrategy_AllValuesHaveUniqueComplexityLevels()
    {
        // Arrange
        var strategies = Enum.GetValues<RagStrategy>();
        var levels = strategies.Select(s => s.GetComplexityLevel()).ToList();

        // Assert
        Assert.Equal(levels.Count, levels.Distinct().Count());
    }

    #endregion
}
