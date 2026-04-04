using Api.BoundedContexts.GameManagement.Domain.Models;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.GameManagement.Domain.Models;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "GameManagement")]
public sealed class SetupChecklistDataTests
{
    #region SetupComponent Tests

    [Fact]
    public void SetupComponent_WithValidData_CreatesInstance()
    {
        // Act
        var component = new SetupComponent("Dice", 2);

        // Assert
        component.Name.Should().Be("Dice");
        component.Quantity.Should().Be(2);
        component.Checked.Should().BeFalse();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void SetupComponent_WithNullOrEmptyName_ThrowsArgumentException(string? name)
    {
        // Act
        var act = () => new SetupComponent(name!, 1);

        // Assert
        act.Should().Throw<ArgumentException>()
            .And.ParamName.Should().Be("name");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-100)]
    public void SetupComponent_WithZeroOrNegativeQuantity_ThrowsArgumentException(int quantity)
    {
        // Act
        var act = () => new SetupComponent("Dice", quantity);

        // Assert
        act.Should().Throw<ArgumentException>()
            .And.ParamName.Should().Be("quantity");
    }

    #endregion

    #region SetupStep Tests

    [Fact]
    public void SetupStep_WithValidData_CreatesInstance()
    {
        // Act
        var step = new SetupStep(1, "Shuffle the deck");

        // Assert
        step.Order.Should().Be(1);
        step.Instruction.Should().Be("Shuffle the deck");
        step.Completed.Should().BeFalse();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void SetupStep_WithNullOrEmptyInstruction_ThrowsArgumentException(string? instruction)
    {
        // Act
        var act = () => new SetupStep(1, instruction!);

        // Assert
        act.Should().Throw<ArgumentException>()
            .And.ParamName.Should().Be("instruction");
    }

    #endregion

    #region SetupChecklistData Constructor Tests

    [Fact]
    public void SetupChecklistData_WithValidData_CreatesInstance()
    {
        // Arrange
        var components = new List<SetupComponent>
        {
            new("Dice", 2),
            new("Cards", 52)
        };
        var steps = new List<SetupStep>
        {
            new(1, "Shuffle the deck"),
            new(2, "Deal 5 cards each")
        };

        // Act
        var checklist = new SetupChecklistData(4, components, steps);

        // Assert
        checklist.PlayerCount.Should().Be(4);
        checklist.Components.Should().HaveCount(2);
        checklist.SetupSteps.Should().HaveCount(2);
    }

    [Fact]
    public void SetupChecklistData_WithNullComponents_DefaultsToEmptyList()
    {
        // Act
        var checklist = new SetupChecklistData(2, null!, new List<SetupStep>());

        // Assert
        checklist.Components.Should().NotBeNull().And.BeEmpty();
    }

    [Fact]
    public void SetupChecklistData_WithNullSteps_DefaultsToEmptyList()
    {
        // Act
        var checklist = new SetupChecklistData(2, new List<SetupComponent>(), null!);

        // Assert
        checklist.SetupSteps.Should().NotBeNull().And.BeEmpty();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-10)]
    public void SetupChecklistData_WithZeroOrNegativePlayerCount_ThrowsArgumentException(int playerCount)
    {
        // Act
        var act = () => new SetupChecklistData(playerCount, new List<SetupComponent>(), new List<SetupStep>());

        // Assert
        act.Should().Throw<ArgumentException>()
            .And.ParamName.Should().Be("playerCount");
    }

    #endregion

    #region ToggleComponent Tests

    [Fact]
    public void ToggleComponent_WithValidIndex_SetsCheckedTrue()
    {
        // Arrange
        var checklist = CreateDefaultChecklist();

        // Act
        checklist.ToggleComponent(0);

        // Assert
        checklist.Components[0].Checked.Should().BeTrue();
    }

    [Fact]
    public void ToggleComponent_CalledTwice_SetsCheckedBackToFalse()
    {
        // Arrange
        var checklist = CreateDefaultChecklist();

        // Act
        checklist.ToggleComponent(0);
        checklist.ToggleComponent(0);

        // Assert
        checklist.Components[0].Checked.Should().BeFalse();
    }

    [Fact]
    public void ToggleComponent_DoesNotAffectOtherComponents()
    {
        // Arrange
        var checklist = CreateDefaultChecklist();

        // Act
        checklist.ToggleComponent(0);

        // Assert
        checklist.Components[1].Checked.Should().BeFalse();
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(5)]
    [InlineData(100)]
    public void ToggleComponent_WithOutOfRangeIndex_ThrowsArgumentOutOfRangeException(int index)
    {
        // Arrange
        var checklist = CreateDefaultChecklist();

        // Act
        var act = () => checklist.ToggleComponent(index);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    #endregion

    #region CompleteStep Tests

    [Fact]
    public void CompleteStep_WithValidIndex_SetsCompletedTrue()
    {
        // Arrange
        var checklist = CreateDefaultChecklist();

        // Act
        checklist.CompleteStep(0);

        // Assert
        checklist.SetupSteps[0].Completed.Should().BeTrue();
    }

    [Fact]
    public void CompleteStep_DoesNotAffectOtherSteps()
    {
        // Arrange
        var checklist = CreateDefaultChecklist();

        // Act
        checklist.CompleteStep(0);

        // Assert
        checklist.SetupSteps[1].Completed.Should().BeFalse();
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(5)]
    [InlineData(100)]
    public void CompleteStep_WithOutOfRangeIndex_ThrowsArgumentOutOfRangeException(int index)
    {
        // Arrange
        var checklist = CreateDefaultChecklist();

        // Act
        var act = () => checklist.CompleteStep(index);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    #endregion

    #region Helpers

    private static SetupChecklistData CreateDefaultChecklist()
    {
        var components = new List<SetupComponent>
        {
            new("Dice", 2),
            new("Cards", 52)
        };
        var steps = new List<SetupStep>
        {
            new(1, "Shuffle the deck"),
            new(2, "Deal 5 cards each")
        };
        return new SetupChecklistData(4, components, steps);
    }

    #endregion
}
