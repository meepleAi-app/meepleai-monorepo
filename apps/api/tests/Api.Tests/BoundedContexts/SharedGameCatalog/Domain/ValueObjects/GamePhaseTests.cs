using Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;

/// <summary>
/// Tests for the GamePhase value object.
/// Issue #3025: Backend 90% Coverage Target - Phase 9
/// </summary>
[Trait("Category", "Unit")]
public sealed class GamePhaseTests
{
    #region Create Factory Tests

    [Fact]
    public void Create_WithValidData_ReturnsGamePhase()
    {
        // Act
        var phase = GamePhase.Create(
            name: "Action Phase",
            description: "Players take their actions",
            order: 1,
            isOptional: false);

        // Assert
        phase.Name.Should().Be("Action Phase");
        phase.Description.Should().Be("Players take their actions");
        phase.Order.Should().Be(1);
        phase.IsOptional.Should().BeFalse();
    }

    [Fact]
    public void Create_WithOptionalPhase_SetsIsOptionalTrue()
    {
        // Act
        var phase = GamePhase.Create(
            name: "Trading Phase",
            description: "Optional trading between players",
            order: 2,
            isOptional: true);

        // Assert
        phase.IsOptional.Should().BeTrue();
    }

    [Fact]
    public void Create_TrimsNameAndDescription()
    {
        // Act
        var phase = GamePhase.Create(
            name: "  Setup Phase  ",
            description: "  Prepare the game board  ",
            order: 1);

        // Assert
        phase.Name.Should().Be("Setup Phase");
        phase.Description.Should().Be("Prepare the game board");
    }

    [Fact]
    public void Create_WithDefaultOptionalValue_IsFalse()
    {
        // Act
        var phase = GamePhase.Create("Phase", "Description", 1);

        // Assert
        phase.IsOptional.Should().BeFalse();
    }

    #endregion

    #region Validation Tests

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithEmptyName_ThrowsArgumentException(string? name)
    {
        // Act
        var action = () => GamePhase.Create(name!, "Description", 1);

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithMessage("*Phase name is required*");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithEmptyDescription_ThrowsArgumentException(string? description)
    {
        // Act
        var action = () => GamePhase.Create("Name", description!, 1);

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithMessage("*Phase description is required*");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-100)]
    public void Create_WithNonPositiveOrder_ThrowsArgumentException(int order)
    {
        // Act
        var action = () => GamePhase.Create("Name", "Description", order);

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithMessage("*Phase order must be positive*");
    }

    #endregion

    #region ToString Tests

    [Fact]
    public void ToString_ReturnsOrderAndName()
    {
        // Arrange
        var phase = GamePhase.Create("Action Phase", "Take actions", 2);

        // Act
        var result = phase.ToString();

        // Assert
        result.Should().Be("2. Action Phase");
    }

    #endregion

    #region Equality Tests

    [Fact]
    public void TwoPhases_WithSameValues_AreEqual()
    {
        // Arrange
        var phase1 = GamePhase.Create("Phase", "Description", 1, false);
        var phase2 = GamePhase.Create("Phase", "Description", 1, false);

        // Assert
        phase1.Should().Be(phase2);
        phase1.Equals(phase2).Should().BeTrue();
    }

    [Fact]
    public void TwoPhases_WithDifferentNames_AreNotEqual()
    {
        // Arrange
        var phase1 = GamePhase.Create("Phase A", "Description", 1);
        var phase2 = GamePhase.Create("Phase B", "Description", 1);

        // Assert
        phase1.Should().NotBe(phase2);
    }

    [Fact]
    public void TwoPhases_WithDifferentOrders_AreNotEqual()
    {
        // Arrange
        var phase1 = GamePhase.Create("Phase", "Description", 1);
        var phase2 = GamePhase.Create("Phase", "Description", 2);

        // Assert
        phase1.Should().NotBe(phase2);
    }

    [Fact]
    public void TwoPhases_WithDifferentOptional_AreNotEqual()
    {
        // Arrange
        var phase1 = GamePhase.Create("Phase", "Description", 1, false);
        var phase2 = GamePhase.Create("Phase", "Description", 1, true);

        // Assert
        phase1.Should().NotBe(phase2);
    }

    [Fact]
    public void TwoPhases_WithSameValues_HaveSameHashCode()
    {
        // Arrange
        var phase1 = GamePhase.Create("Phase", "Description", 1);
        var phase2 = GamePhase.Create("Phase", "Description", 1);

        // Assert
        phase1.GetHashCode().Should().Be(phase2.GetHashCode());
    }

    #endregion
}
