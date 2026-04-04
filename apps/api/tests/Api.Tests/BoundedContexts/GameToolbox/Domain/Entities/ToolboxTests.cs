using Api.BoundedContexts.GameToolbox.Domain.Entities;
using Api.BoundedContexts.GameToolbox.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.GameToolbox.Domain.Entities;

/// <summary>
/// Tests for the Toolbox aggregate root.
/// Epic #412: Game Toolbox
/// </summary>
[Trait("Category", "Unit")]
public sealed class ToolboxTests
{
    #region Create Tests

    [Fact]
    public void Create_WithValidName_ReturnsToolbox()
    {
        var gameId = Guid.NewGuid();
        var toolbox = Toolbox.Create("Catan Toolbox", gameId);

        toolbox.Name.Should().Be("Catan Toolbox");
        toolbox.GameId.Should().Be(gameId);
        toolbox.Mode.Should().Be(ToolboxMode.Freeform);
        toolbox.Tools.Should().BeEmpty();
        toolbox.Phases.Should().BeEmpty();
        toolbox.CurrentPhaseId.Should().BeNull();
        toolbox.IsDeleted.Should().BeFalse();
        toolbox.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void Create_WithPhasedMode_SetsMode()
    {
        var toolbox = Toolbox.Create("Test", mode: ToolboxMode.Phased);
        toolbox.Mode.Should().Be(ToolboxMode.Phased);
    }

    [Fact]
    public void Create_WithoutGameId_IsStandalone()
    {
        var toolbox = Toolbox.Create("Standalone");
        toolbox.GameId.Should().BeNull();
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData(null)]
    public void Create_WithInvalidName_Throws(string? name)
    {
        var act = () => Toolbox.Create(name!);
        act.Should().Throw<ArgumentException>().WithParameterName("name");
    }

    #endregion

    #region Tool Management Tests

    [Fact]
    public void AddTool_AddsToCollection()
    {
        var toolbox = Toolbox.Create("Test");
        var tool = toolbox.AddTool("DiceRoller", """{"formula":"2d6"}""");

        toolbox.Tools.Should().HaveCount(1);
        tool.Type.Should().Be("DiceRoller");
        tool.Config.Should().Be("""{"formula":"2d6"}""");
        tool.Order.Should().Be(0);
        tool.IsEnabled.Should().BeTrue();
        tool.ToolboxId.Should().Be(toolbox.Id);
    }

    [Fact]
    public void AddTool_SecondTool_IncrementsOrder()
    {
        var toolbox = Toolbox.Create("Test");
        toolbox.AddTool("DiceRoller", "{}");
        var second = toolbox.AddTool("ScoreTracker", "{}");

        second.Order.Should().Be(1);
    }

    [Fact]
    public void AddTool_UpdatesTimestamp()
    {
        var toolbox = Toolbox.Create("Test");
        var before = toolbox.UpdatedAt;

        toolbox.AddTool("DiceRoller", "{}");

        toolbox.UpdatedAt.Should().BeOnOrAfter(before);
    }

    [Fact]
    public void RemoveTool_RemovesFromCollection()
    {
        var toolbox = Toolbox.Create("Test");
        var tool = toolbox.AddTool("DiceRoller", "{}");

        toolbox.RemoveTool(tool.Id);

        toolbox.Tools.Should().BeEmpty();
    }

    [Fact]
    public void RemoveTool_NotFound_Throws()
    {
        var toolbox = Toolbox.Create("Test");
        var act = () => toolbox.RemoveTool(Guid.NewGuid());
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void ReorderTools_UpdatesOrder()
    {
        var toolbox = Toolbox.Create("Test");
        var t1 = toolbox.AddTool("DiceRoller", "{}");
        var t2 = toolbox.AddTool("ScoreTracker", "{}");
        var t3 = toolbox.AddTool("TurnManager", "{}");

        toolbox.ReorderTools([t3.Id, t1.Id, t2.Id]);

        toolbox.Tools.First(t => t.Id == t3.Id).Order.Should().Be(0);
        toolbox.Tools.First(t => t.Id == t1.Id).Order.Should().Be(1);
        toolbox.Tools.First(t => t.Id == t2.Id).Order.Should().Be(2);
    }

    #endregion

    #region SharedContext Tests

    [Fact]
    public void UpdateSharedContext_UpdatesContext()
    {
        var toolbox = Toolbox.Create("Test");
        var ctx = new SharedContext
        {
            Players = [new("Marco", "red"), new("Lucia", "blue")],
            CurrentPlayerIndex = 0,
            CurrentRound = 1
        };

        toolbox.UpdateSharedContext(ctx);

        toolbox.SharedContext.Players.Should().HaveCount(2);
        toolbox.SharedContext.CurrentPlayer!.Name.Should().Be("Marco");
    }

    [Fact]
    public void UpdateSharedContext_WithNull_Throws()
    {
        var toolbox = Toolbox.Create("Test");
        var act = () => toolbox.UpdateSharedContext(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    #endregion

    #region Mode Tests

    [Fact]
    public void UpdateMode_ToPhasedWithPhases_SetsCurrentPhase()
    {
        var toolbox = Toolbox.Create("Test");
        var phase = toolbox.AddPhase("Setup");

        toolbox.UpdateMode(ToolboxMode.Phased);

        toolbox.Mode.Should().Be(ToolboxMode.Phased);
        toolbox.CurrentPhaseId.Should().Be(phase.Id);
    }

    [Fact]
    public void UpdateMode_ToFreeform_ClearsCurrentPhase()
    {
        var toolbox = Toolbox.Create("Test", mode: ToolboxMode.Phased);
        toolbox.AddPhase("Setup");

        toolbox.UpdateMode(ToolboxMode.Freeform);

        toolbox.CurrentPhaseId.Should().BeNull();
    }

    [Fact]
    public void UpdateMode_ToPhasedWithNoPhases_CurrentPhaseNull()
    {
        var toolbox = Toolbox.Create("Test");
        toolbox.UpdateMode(ToolboxMode.Phased);
        toolbox.CurrentPhaseId.Should().BeNull();
    }

    #endregion

    #region Phase Management Tests

    [Fact]
    public void AddPhase_AddsToCollection()
    {
        var toolbox = Toolbox.Create("Test");
        var phase = toolbox.AddPhase("Setup", [Guid.NewGuid()]);

        toolbox.Phases.Should().HaveCount(1);
        phase.Name.Should().Be("Setup");
        phase.Order.Should().Be(0);
        phase.ActiveToolIds.Should().HaveCount(1);
        phase.ToolboxId.Should().Be(toolbox.Id);
    }

    [Fact]
    public void AddPhase_InPhasedMode_SetsAsCurrentIfFirst()
    {
        var toolbox = Toolbox.Create("Test", mode: ToolboxMode.Phased);
        var phase = toolbox.AddPhase("Setup");

        toolbox.CurrentPhaseId.Should().Be(phase.Id);
    }

    [Fact]
    public void AddPhase_InFreeform_DoesNotSetCurrent()
    {
        var toolbox = Toolbox.Create("Test");
        toolbox.AddPhase("Setup");

        toolbox.CurrentPhaseId.Should().BeNull();
    }

    [Fact]
    public void RemovePhase_RemovesFromCollection()
    {
        var toolbox = Toolbox.Create("Test");
        var phase = toolbox.AddPhase("Setup");

        toolbox.RemovePhase(phase.Id);

        toolbox.Phases.Should().BeEmpty();
    }

    [Fact]
    public void RemovePhase_CurrentPhase_AdvancesToNext()
    {
        var toolbox = Toolbox.Create("Test", mode: ToolboxMode.Phased);
        var p1 = toolbox.AddPhase("Phase 1");
        var p2 = toolbox.AddPhase("Phase 2");

        toolbox.RemovePhase(p1.Id);

        toolbox.CurrentPhaseId.Should().Be(p2.Id);
    }

    [Fact]
    public void RemovePhase_NotFound_Throws()
    {
        var toolbox = Toolbox.Create("Test");
        var act = () => toolbox.RemovePhase(Guid.NewGuid());
        act.Should().Throw<InvalidOperationException>();
    }

    #endregion

    #region AdvancePhase Tests

    [Fact]
    public void AdvancePhase_CyclesToNextPhase()
    {
        var toolbox = Toolbox.Create("Test", mode: ToolboxMode.Phased);
        var p1 = toolbox.AddPhase("Phase 1");
        var p2 = toolbox.AddPhase("Phase 2");

        toolbox.CurrentPhaseId.Should().Be(p1.Id);

        var next = toolbox.AdvancePhase();

        toolbox.CurrentPhaseId.Should().Be(p2.Id);
        next.Name.Should().Be("Phase 2");
    }

    [Fact]
    public void AdvancePhase_WrapsAround_AdvancesRound()
    {
        var toolbox = Toolbox.Create("Test", mode: ToolboxMode.Phased);
        toolbox.AddPhase("Phase 1");
        toolbox.AddPhase("Phase 2");

        toolbox.SharedContext.CurrentRound.Should().Be(1);

        toolbox.AdvancePhase(); // → Phase 2
        toolbox.AdvancePhase(); // → Phase 1, round 2

        toolbox.SharedContext.CurrentRound.Should().Be(2);
    }

    [Fact]
    public void AdvancePhase_InFreeform_Throws()
    {
        var toolbox = Toolbox.Create("Test");
        var act = () => toolbox.AdvancePhase();
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Freeform*");
    }

    [Fact]
    public void AdvancePhase_NoPhases_Throws()
    {
        var toolbox = Toolbox.Create("Test", mode: ToolboxMode.Phased);
        var act = () => toolbox.AdvancePhase();
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*No phases*");
    }

    #endregion

    #region SoftDelete Tests

    [Fact]
    public void SoftDelete_SetsFlags()
    {
        var toolbox = Toolbox.Create("Test");

        toolbox.SoftDelete();

        toolbox.IsDeleted.Should().BeTrue();
        toolbox.DeletedAt.Should().NotBeNull();
        toolbox.DeletedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(2));
    }

    #endregion
}
