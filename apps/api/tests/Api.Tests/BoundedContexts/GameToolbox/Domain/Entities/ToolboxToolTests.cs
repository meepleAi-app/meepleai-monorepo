using Api.BoundedContexts.GameToolbox.Domain.Entities;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.GameToolbox.Domain.Entities;

/// <summary>
/// Tests for the ToolboxTool entity.
/// Epic #412: Game Toolbox
/// </summary>
[Trait("Category", "Unit")]
public sealed class ToolboxToolTests
{
    [Fact]
    public void Create_WithValidType_ReturnsTool()
    {
        var tool = ToolboxTool.Create("DiceRoller", """{"formula":"2d6"}""", 0);

        tool.Type.Should().Be("DiceRoller");
        tool.Config.Should().Be("""{"formula":"2d6"}""");
        tool.Order.Should().Be(0);
        tool.IsEnabled.Should().BeTrue();
        tool.State.Should().Be("{}");
        tool.Id.Should().NotBeEmpty();
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData(null)]
    public void Create_WithInvalidType_Throws(string? type)
    {
        var act = () => ToolboxTool.Create(type!, "{}", 0);
        act.Should().Throw<ArgumentException>().WithParameterName("type");
    }

    [Fact]
    public void Enable_SetsTrue()
    {
        var tool = ToolboxTool.Create("DiceRoller", "{}", 0);
        tool.Disable();
        tool.IsEnabled.Should().BeFalse();

        tool.Enable();
        tool.IsEnabled.Should().BeTrue();
    }

    [Fact]
    public void Disable_SetsFalse()
    {
        var tool = ToolboxTool.Create("DiceRoller", "{}", 0);
        tool.Disable();
        tool.IsEnabled.Should().BeFalse();
    }

    [Fact]
    public void UpdateConfig_ChangesConfig()
    {
        var tool = ToolboxTool.Create("DiceRoller", "{}", 0);
        tool.UpdateConfig("""{"formula":"3d8"}""");
        tool.Config.Should().Be("""{"formula":"3d8"}""");
    }

    [Fact]
    public void UpdateState_ChangesState()
    {
        var tool = ToolboxTool.Create("DiceRoller", "{}", 0);
        tool.UpdateState("""{"lastRoll":7}""");
        tool.State.Should().Be("""{"lastRoll":7}""");
    }

    [Fact]
    public void SetOrder_ChangesOrder()
    {
        var tool = ToolboxTool.Create("DiceRoller", "{}", 0);
        tool.SetOrder(5);
        tool.Order.Should().Be(5);
    }
}
