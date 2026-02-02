using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.Tests.Constants;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Domain.ValueObjects;

/// <summary>
/// Unit tests for RagStrategy enum and extensions.
/// Issue #3436: TierStrategyAccess validation service.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
public class RagStrategyTests
{
    #region Enum Value Tests

    [Fact]
    public void RagStrategy_HasCorrectValues()
    {
        // Assert
        Assert.Equal(0, (int)RagStrategy.None);
        Assert.Equal(1, (int)RagStrategy.Fast);
        Assert.Equal(2, (int)RagStrategy.Balanced);
        Assert.Equal(3, (int)RagStrategy.Precise);
        Assert.Equal(4, (int)RagStrategy.Custom);
    }

    [Fact]
    public void RagStrategy_AllValuesAreDefined()
    {
        // Arrange
        var expectedValues = new[] { RagStrategy.None, RagStrategy.Fast, RagStrategy.Balanced, RagStrategy.Precise, RagStrategy.Custom };

        // Act
        var actualValues = Enum.GetValues<RagStrategy>();

        // Assert
        Assert.Equal(expectedValues.Length, actualValues.Length);
        foreach (var expected in expectedValues)
        {
            Assert.Contains(expected, actualValues);
        }
    }

    #endregion

    #region GetDisplayName Tests

    [Theory]
    [InlineData(RagStrategy.None, "None")]
    [InlineData(RagStrategy.Fast, "Fast")]
    [InlineData(RagStrategy.Balanced, "Balanced")]
    [InlineData(RagStrategy.Precise, "Precise")]
    [InlineData(RagStrategy.Custom, "Custom")]
    public void GetDisplayName_ReturnsCorrectName(RagStrategy strategy, string expectedName)
    {
        // Act
        var displayName = strategy.GetDisplayName();

        // Assert
        Assert.Equal(expectedName, displayName);
    }

    #endregion

    #region GetDescription Tests

    [Fact]
    public void GetDescription_None_ReturnsNoAccessMessage()
    {
        // Act
        var description = RagStrategy.None.GetDescription();

        // Assert
        Assert.Contains("No RAG strategy", description);
    }

    [Fact]
    public void GetDescription_Fast_DescribesSpeed()
    {
        // Act
        var description = RagStrategy.Fast.GetDescription();

        // Assert
        Assert.Contains("speed", description, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void GetDescription_Balanced_DescribesBalance()
    {
        // Act
        var description = RagStrategy.Balanced.GetDescription();

        // Assert
        Assert.Contains("balancing", description, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void GetDescription_Precise_DescribesAccuracy()
    {
        // Act
        var description = RagStrategy.Precise.GetDescription();

        // Assert
        Assert.Contains("accuracy", description, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void GetDescription_Custom_DescribesUserDefined()
    {
        // Act
        var description = RagStrategy.Custom.GetDescription();

        // Assert
        Assert.Contains("User-defined", description);
    }

    #endregion

    #region RequiresAdmin Tests

    [Theory]
    [InlineData(RagStrategy.None, false)]
    [InlineData(RagStrategy.Fast, false)]
    [InlineData(RagStrategy.Balanced, false)]
    [InlineData(RagStrategy.Precise, false)]
    [InlineData(RagStrategy.Custom, true)]
    public void RequiresAdmin_ReturnsCorrectValue(RagStrategy strategy, bool expectedRequiresAdmin)
    {
        // Act
        var requiresAdmin = strategy.RequiresAdmin();

        // Assert
        Assert.Equal(expectedRequiresAdmin, requiresAdmin);
    }

    [Fact]
    public void RequiresAdmin_OnlyCustomRequiresAdmin()
    {
        // Arrange
        var strategies = Enum.GetValues<RagStrategy>();

        // Act & Assert
        foreach (var strategy in strategies)
        {
            var requiresAdmin = strategy.RequiresAdmin();
            Assert.Equal(strategy == RagStrategy.Custom, requiresAdmin);
        }
    }

    #endregion

    #region ParseOrDefault Tests

    [Theory]
    [InlineData("Fast", RagStrategy.Fast)]
    [InlineData("fast", RagStrategy.Fast)]
    [InlineData("FAST", RagStrategy.Fast)]
    [InlineData("Balanced", RagStrategy.Balanced)]
    [InlineData("balanced", RagStrategy.Balanced)]
    [InlineData("Precise", RagStrategy.Precise)]
    [InlineData("Custom", RagStrategy.Custom)]
    [InlineData("None", RagStrategy.None)]
    public void ParseOrDefault_ValidInput_ReturnsCorrectStrategy(string input, RagStrategy expected)
    {
        // Act
        var result = RagStrategyExtensions.ParseOrDefault(input);

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Invalid")]
    [InlineData("FastAndFurious")]
    public void ParseOrDefault_InvalidInput_ReturnsNone(string? input)
    {
        // Act
        var result = RagStrategyExtensions.ParseOrDefault(input);

        // Assert
        Assert.Equal(RagStrategy.None, result);
    }

    #endregion
}
