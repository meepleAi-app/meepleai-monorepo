using Api.BoundedContexts.GameToolbox.Domain.Entities;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.GameToolbox.Domain.Entities;

/// <summary>
/// Tests for the Phase entity.
/// Epic #412: Game Toolbox
/// </summary>
[Trait("Category", "Unit")]
public sealed class PhaseTests
{
    [Fact]
    public void Create_WithValidName_ReturnsPhase()
    {
        var toolIds = new List<Guid> { Guid.NewGuid(), Guid.NewGuid() };
        var phase = Phase.Create("Setup", 0, toolIds);

        phase.Name.Should().Be("Setup");
        phase.Order.Should().Be(0);
        phase.ActiveToolIds.Should().HaveCount(2);
        phase.Id.Should().NotBeEmpty();
    }

    [Fact]
    public void Create_WithoutActiveToolIds_DefaultsEmpty()
    {
        var phase = Phase.Create("Setup", 0);
        phase.ActiveToolIds.Should().BeEmpty();
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData(null)]
    public void Create_WithInvalidName_Throws(string? name)
    {
        var act = () => Phase.Create(name!, 0);
        act.Should().Throw<ArgumentException>().WithParameterName("name");
    }

    [Fact]
    public void UpdateName_ChangesName()
    {
        var phase = Phase.Create("Old", 0);
        phase.UpdateName("New");
        phase.Name.Should().Be("New");
    }

    [Fact]
    public void UpdateName_WithEmpty_Throws()
    {
        var phase = Phase.Create("Valid", 0);
        var act = () => phase.UpdateName("");
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void IsToolActive_ReturnsTrueForActiveTools()
    {
        var toolId = Guid.NewGuid();
        var phase = Phase.Create("Setup", 0, [toolId]);

        phase.IsToolActive(toolId).Should().BeTrue();
        phase.IsToolActive(Guid.NewGuid()).Should().BeFalse();
    }

    [Fact]
    public void SetActiveTools_ReplacesToolIds()
    {
        var phase = Phase.Create("Setup", 0, [Guid.NewGuid()]);
        var newIds = new List<Guid> { Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid() };

        phase.SetActiveTools(newIds);

        phase.ActiveToolIds.Should().HaveCount(3);
    }

    [Fact]
    public void SetOrder_ChangesOrder()
    {
        var phase = Phase.Create("Setup", 0);
        phase.SetOrder(3);
        phase.Order.Should().Be(3);
    }
}
