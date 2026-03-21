using Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;

/// <summary>
/// Tests for the VictoryConditions value object.
/// Issue #3025: Backend 90% Coverage Target - Phase 9
/// </summary>
[Trait("Category", "Unit")]
public sealed class VictoryConditionsTests
{
    #region Create Factory Tests

    [Fact]
    public void Create_WithPrimaryOnly_ReturnsVictoryConditions()
    {
        // Act
        var conditions = VictoryConditions.Create(
            primary: "Reach 10 victory points");

        // Assert
        conditions.Primary.Should().Be("Reach 10 victory points");
        conditions.Alternatives.Should().BeEmpty();
        conditions.IsPointBased.Should().BeFalse();
        conditions.TargetPoints.Should().BeNull();
    }

    [Fact]
    public void Create_WithAlternatives_IncludesAlternatives()
    {
        // Arrange
        var alternatives = new List<string>
        {
            "Control all territories",
            "Eliminate all opponents"
        };

        // Act
        var conditions = VictoryConditions.Create(
            primary: "Reach 10 victory points",
            alternatives: alternatives);

        // Assert
        conditions.Alternatives.Should().HaveCount(2);
        conditions.Alternatives.Should().Contain("Control all territories");
        conditions.Alternatives.Should().Contain("Eliminate all opponents");
    }

    [Fact]
    public void Create_WithPointBasedAndTarget_SetsValues()
    {
        // Act
        var conditions = VictoryConditions.Create(
            primary: "First to reach target points wins",
            isPointBased: true,
            targetPoints: 100);

        // Assert
        conditions.IsPointBased.Should().BeTrue();
        conditions.TargetPoints.Should().Be(100);
    }

    [Fact]
    public void Create_TrimsPrimary()
    {
        // Act
        var conditions = VictoryConditions.Create("  Score the most points  ");

        // Assert
        conditions.Primary.Should().Be("Score the most points");
    }

    [Fact]
    public void Create_WithNullAlternatives_UsesEmptyList()
    {
        // Act
        var conditions = VictoryConditions.Create("Primary", alternatives: null);

        // Assert
        conditions.Alternatives.Should().BeEmpty();
    }

    #endregion

    #region Validation Tests

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithEmptyPrimary_ThrowsArgumentException(string? primary)
    {
        // Act
        var action = () => VictoryConditions.Create(primary!);

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithMessage("*Primary victory condition is required*");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-100)]
    public void Create_WithPointBasedAndNonPositiveTarget_ThrowsArgumentException(int targetPoints)
    {
        // Act
        var action = () => VictoryConditions.Create(
            primary: "Score points",
            isPointBased: true,
            targetPoints: targetPoints);

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithMessage("*Target points must be positive if point-based*");
    }

    [Fact]
    public void Create_WithPointBasedAndNullTarget_Succeeds()
    {
        // Point-based without specific target is allowed (e.g., "most points wins")
        // Act
        var conditions = VictoryConditions.Create(
            primary: "Most points wins",
            isPointBased: true,
            targetPoints: null);

        // Assert
        conditions.IsPointBased.Should().BeTrue();
        conditions.TargetPoints.Should().BeNull();
    }

    #endregion

    #region Empty Static Property Tests

    [Fact]
    public void Empty_ReturnsDefaultVictoryConditions()
    {
        // Act
        var conditions = VictoryConditions.Empty;

        // Assert
        conditions.Primary.Should().Be("Not specified");
        conditions.Alternatives.Should().BeEmpty();
        conditions.IsPointBased.Should().BeFalse();
        conditions.TargetPoints.Should().BeNull();
    }

    #endregion

    #region Equality Tests

    [Fact]
    public void TwoConditions_WithSameValues_AreEqual()
    {
        // Arrange
        var conditions1 = VictoryConditions.Create("Primary", isPointBased: true, targetPoints: 10);
        var conditions2 = VictoryConditions.Create("Primary", isPointBased: true, targetPoints: 10);

        // Assert
        conditions1.Should().Be(conditions2);
    }

    [Fact]
    public void TwoConditions_WithDifferentPrimary_AreNotEqual()
    {
        // Arrange
        var conditions1 = VictoryConditions.Create("Primary A");
        var conditions2 = VictoryConditions.Create("Primary B");

        // Assert
        conditions1.Should().NotBe(conditions2);
    }

    [Fact]
    public void TwoConditions_WithDifferentPointBased_AreNotEqual()
    {
        // Arrange
        var conditions1 = VictoryConditions.Create("Primary", isPointBased: false);
        var conditions2 = VictoryConditions.Create("Primary", isPointBased: true);

        // Assert
        conditions1.Should().NotBe(conditions2);
    }

    [Fact]
    public void TwoConditions_WithDifferentTargetPoints_AreNotEqual()
    {
        // Arrange
        var conditions1 = VictoryConditions.Create("Primary", isPointBased: true, targetPoints: 10);
        var conditions2 = VictoryConditions.Create("Primary", isPointBased: true, targetPoints: 20);

        // Assert
        conditions1.Should().NotBe(conditions2);
    }

    [Fact]
    public void TwoConditions_WithDifferentAlternatives_AreNotEqual()
    {
        // Arrange
        var conditions1 = VictoryConditions.Create("Primary", new List<string> { "Alt 1" });
        var conditions2 = VictoryConditions.Create("Primary", new List<string> { "Alt 2" });

        // Assert
        conditions1.Should().NotBe(conditions2);
    }

    [Fact]
    public void TwoConditions_WithSameAlternatives_AreEqual()
    {
        // Arrange
        var conditions1 = VictoryConditions.Create("Primary", new List<string> { "Alt 1", "Alt 2" });
        var conditions2 = VictoryConditions.Create("Primary", new List<string> { "Alt 1", "Alt 2" });

        // Assert
        conditions1.Should().Be(conditions2);
    }

    #endregion
}
