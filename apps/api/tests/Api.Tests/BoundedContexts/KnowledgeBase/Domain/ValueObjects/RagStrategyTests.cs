using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.Tests.Constants;
using Xunit;
using FluentAssertions;

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
        ((int)RagStrategy.None).Should().Be(0);
        ((int)RagStrategy.Fast).Should().Be(1);
        ((int)RagStrategy.Balanced).Should().Be(2);
        ((int)RagStrategy.Precise).Should().Be(3);
        ((int)RagStrategy.Custom).Should().Be(4);
    }

    [Fact]
    public void RagStrategy_AllValuesAreDefined()
    {
        // Arrange
        var expectedValues = new[] { RagStrategy.None, RagStrategy.Fast, RagStrategy.Balanced, RagStrategy.Precise, RagStrategy.Custom };

        // Act
        var actualValues = Enum.GetValues<RagStrategy>();

        // Assert
        actualValues.Length.Should().Be(expectedValues.Length);
        foreach (var expected in expectedValues)
        {
            actualValues.Should().Contain(expected);
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
        displayName.Should().Be(expectedName);
    }

    #endregion

    #region GetDescription Tests

    [Fact]
    public void GetDescription_None_ReturnsNoAccessMessage()
    {
        // Act
        var description = RagStrategy.None.GetDescription();

        // Assert
        description.Should().Contain("No RAG strategy");
    }

    [Fact]
    public void GetDescription_Fast_DescribesSpeed()
    {
        // Act
        var description = RagStrategy.Fast.GetDescription();

        // Assert
        description.Should().ContainEquivalentOf("speed");
    }

    [Fact]
    public void GetDescription_Balanced_DescribesBalance()
    {
        // Act
        var description = RagStrategy.Balanced.GetDescription();

        // Assert
        description.Should().ContainEquivalentOf("balancing");
    }

    [Fact]
    public void GetDescription_Precise_DescribesAccuracy()
    {
        // Act
        var description = RagStrategy.Precise.GetDescription();

        // Assert
        description.Should().ContainEquivalentOf("accuracy");
    }

    [Fact]
    public void GetDescription_Custom_DescribesUserDefined()
    {
        // Act
        var description = RagStrategy.Custom.GetDescription();

        // Assert
        description.Should().Contain("User-defined");
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
        requiresAdmin.Should().Be(expectedRequiresAdmin);
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
            requiresAdmin.Should().Be(strategy == RagStrategy.Custom);
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
        result.Should().Be(expected);
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
        result.Should().Be(RagStrategy.None);
    }

    #endregion
}
