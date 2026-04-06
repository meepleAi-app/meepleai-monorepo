using Api.BoundedContexts.GameToolbox.Application.Commands;
using Api.BoundedContexts.GameToolbox.Application.Validators;
using Api.Tests.Constants;
using FluentValidation.TestHelper;
using Xunit;

namespace Api.Tests.BoundedContexts.GameToolbox.Application.Validators;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "GameToolbox")]
public sealed class ToolboxValidatorsTests
{
    // ── CreateToolboxCommandValidator ─────────────────────────────────────────

    private readonly CreateToolboxCommandValidator _createValidator = new();

    [Fact]
    public void CreateToolbox_ValidCommand_PassesValidation()
    {
        var cmd = new CreateToolboxCommand("My Toolbox", Guid.NewGuid(), "Freeform");
        _createValidator.TestValidate(cmd).ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void CreateToolbox_EmptyName_FailsValidation()
    {
        var cmd = new CreateToolboxCommand("", Guid.NewGuid(), "Freeform");
        _createValidator.TestValidate(cmd).ShouldHaveValidationErrorFor(x => x.Name);
    }

    [Fact]
    public void CreateToolbox_NameTooLong_FailsValidation()
    {
        var cmd = new CreateToolboxCommand(new string('A', 201), Guid.NewGuid(), "Freeform");
        _createValidator.TestValidate(cmd).ShouldHaveValidationErrorFor(x => x.Name);
    }

    [Theory]
    [InlineData("Freeform")]
    [InlineData("Phased")]
    public void CreateToolbox_ValidMode_PassesValidation(string mode)
    {
        var cmd = new CreateToolboxCommand("Toolbox", null, mode);
        _createValidator.TestValidate(cmd).ShouldNotHaveValidationErrorFor(x => x.Mode);
    }

    [Fact]
    public void CreateToolbox_InvalidMode_FailsValidation()
    {
        var cmd = new CreateToolboxCommand("Toolbox", null, "InvalidMode");
        var result = _createValidator.TestValidate(cmd);
        result.ShouldHaveValidationErrorFor(x => x.Mode)
              .WithErrorMessage("Mode must be 'Freeform' or 'Phased'.");
    }

    // ── UpdateToolboxModeCommandValidator ────────────────────────────────────

    private readonly UpdateToolboxModeCommandValidator _updateModeValidator = new();

    [Fact]
    public void UpdateToolboxMode_ValidCommand_PassesValidation()
    {
        var cmd = new UpdateToolboxModeCommand(Guid.NewGuid(), "Phased");
        _updateModeValidator.TestValidate(cmd).ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void UpdateToolboxMode_EmptyToolboxId_FailsValidation()
    {
        var cmd = new UpdateToolboxModeCommand(Guid.Empty, "Phased");
        _updateModeValidator.TestValidate(cmd).ShouldHaveValidationErrorFor(x => x.ToolboxId);
    }

    [Fact]
    public void UpdateToolboxMode_InvalidMode_FailsValidation()
    {
        var cmd = new UpdateToolboxModeCommand(Guid.NewGuid(), "Random");
        _updateModeValidator.TestValidate(cmd).ShouldHaveValidationErrorFor(x => x.Mode);
    }

    // ── AddToolToToolboxCommandValidator ─────────────────────────────────────

    private readonly AddToolToToolboxCommandValidator _addToolValidator = new();

    [Theory]
    [InlineData("DiceRoller")]
    [InlineData("ScoreTracker")]
    [InlineData("TurnManager")]
    [InlineData("ResourceManager")]
    [InlineData("Notes")]
    [InlineData("Whiteboard")]
    [InlineData("CardDeck")]
    public void AddToolToToolbox_ValidToolType_PassesValidation(string toolType)
    {
        var cmd = new AddToolToToolboxCommand(Guid.NewGuid(), toolType);
        _addToolValidator.TestValidate(cmd).ShouldNotHaveValidationErrorFor(x => x.Type);
    }

    [Fact]
    public void AddToolToToolbox_InvalidToolType_FailsValidation()
    {
        var cmd = new AddToolToToolboxCommand(Guid.NewGuid(), "FakeTool");
        _addToolValidator.TestValidate(cmd).ShouldHaveValidationErrorFor(x => x.Type);
    }

    [Fact]
    public void AddToolToToolbox_EmptyToolboxId_FailsValidation()
    {
        var cmd = new AddToolToToolboxCommand(Guid.Empty, "DiceRoller");
        _addToolValidator.TestValidate(cmd).ShouldHaveValidationErrorFor(x => x.ToolboxId);
    }

    // ── AddPhaseCommandValidator ──────────────────────────────────────────────

    private readonly AddPhaseCommandValidator _addPhaseValidator = new();

    [Fact]
    public void AddPhase_ValidCommand_PassesValidation()
    {
        var cmd = new AddPhaseCommand(Guid.NewGuid(), "Setup");
        _addPhaseValidator.TestValidate(cmd).ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void AddPhase_EmptyName_FailsValidation()
    {
        var cmd = new AddPhaseCommand(Guid.NewGuid(), "");
        _addPhaseValidator.TestValidate(cmd).ShouldHaveValidationErrorFor(x => x.Name);
    }

    [Fact]
    public void AddPhase_NameTooLong_FailsValidation()
    {
        var cmd = new AddPhaseCommand(Guid.NewGuid(), new string('B', 101));
        _addPhaseValidator.TestValidate(cmd).ShouldHaveValidationErrorFor(x => x.Name);
    }

    // ── DrawCardsCommandValidator ─────────────────────────────────────────────

    private readonly DrawCardsCommandValidator _drawCardsValidator = new();

    [Fact]
    public void DrawCards_ValidCommand_PassesValidation()
    {
        var cmd = new DrawCardsCommand(Guid.NewGuid(), Guid.NewGuid(), 3);
        _drawCardsValidator.TestValidate(cmd).ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void DrawCards_EmptyToolboxId_FailsValidation()
    {
        var cmd = new DrawCardsCommand(Guid.Empty, Guid.NewGuid(), 1);
        _drawCardsValidator.TestValidate(cmd).ShouldHaveValidationErrorFor(x => x.ToolboxId);
    }

    [Fact]
    public void DrawCards_EmptyDeckId_FailsValidation()
    {
        var cmd = new DrawCardsCommand(Guid.NewGuid(), Guid.Empty, 1);
        _drawCardsValidator.TestValidate(cmd).ShouldHaveValidationErrorFor(x => x.DeckId);
    }

    [Fact]
    public void DrawCards_ZeroCount_FailsValidation()
    {
        var cmd = new DrawCardsCommand(Guid.NewGuid(), Guid.NewGuid(), 0);
        _drawCardsValidator.TestValidate(cmd).ShouldHaveValidationErrorFor(x => x.Count);
    }

    [Fact]
    public void DrawCards_CountOver52_FailsValidation()
    {
        var cmd = new DrawCardsCommand(Guid.NewGuid(), Guid.NewGuid(), 53);
        _drawCardsValidator.TestValidate(cmd).ShouldHaveValidationErrorFor(x => x.Count);
    }
}
