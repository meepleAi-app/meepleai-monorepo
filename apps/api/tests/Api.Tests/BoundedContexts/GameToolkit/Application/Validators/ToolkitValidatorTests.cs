using Api.BoundedContexts.GameToolkit.Application.Commands;
using Api.BoundedContexts.GameToolkit.Application.Validators;
using Api.BoundedContexts.GameToolkit.Domain.Enums;
using Api.Tests.Constants;
using FluentValidation.TestHelper;
using Xunit;

namespace Api.Tests.BoundedContexts.GameToolkit.Application.Validators;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "GameToolkit")]
public class ToolkitValidatorTests
{
    // ========================================================================
    // CreateToolkitCommandValidator
    // ========================================================================

    [Fact]
    public void CreateToolkit_ValidCommand_PassesValidation()
    {
        var validator = new CreateToolkitCommandValidator();
        var command = new CreateToolkitCommand(Guid.NewGuid(), "My Toolkit", Guid.NewGuid());

        var result = validator.TestValidate(command);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void CreateToolkit_EmptyGameId_FailsValidation()
    {
        var validator = new CreateToolkitCommandValidator();
        var command = new CreateToolkitCommand(Guid.Empty, "My Toolkit", Guid.NewGuid());

        var result = validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.GameId);
    }

    [Fact]
    public void CreateToolkit_EmptyName_FailsValidation()
    {
        var validator = new CreateToolkitCommandValidator();
        var command = new CreateToolkitCommand(Guid.NewGuid(), "", Guid.NewGuid());

        var result = validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.Name);
    }

    [Fact]
    public void CreateToolkit_NameTooLong_FailsValidation()
    {
        var validator = new CreateToolkitCommandValidator();
        var command = new CreateToolkitCommand(Guid.NewGuid(), new string('X', 201), Guid.NewGuid());

        var result = validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.Name);
    }

    [Fact]
    public void CreateToolkit_EmptyUserId_FailsValidation()
    {
        var validator = new CreateToolkitCommandValidator();
        var command = new CreateToolkitCommand(Guid.NewGuid(), "Test", Guid.Empty);

        var result = validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.CreatedByUserId);
    }

    // ========================================================================
    // UpdateToolkitCommandValidator
    // ========================================================================

    [Fact]
    public void UpdateToolkit_ValidCommand_PassesValidation()
    {
        var validator = new UpdateToolkitCommandValidator();
        var command = new UpdateToolkitCommand(Guid.NewGuid(), "Updated");

        var result = validator.TestValidate(command);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void UpdateToolkit_EmptyToolkitId_FailsValidation()
    {
        var validator = new UpdateToolkitCommandValidator();
        var command = new UpdateToolkitCommand(Guid.Empty, "Test");

        var result = validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.ToolkitId);
    }

    [Fact]
    public void UpdateToolkit_NameTooLong_FailsValidation()
    {
        var validator = new UpdateToolkitCommandValidator();
        var command = new UpdateToolkitCommand(Guid.NewGuid(), new string('X', 201));

        var result = validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.Name);
    }

    [Fact]
    public void UpdateToolkit_NullName_PassesValidation()
    {
        var validator = new UpdateToolkitCommandValidator();
        var command = new UpdateToolkitCommand(Guid.NewGuid(), null);

        var result = validator.TestValidate(command);

        result.ShouldNotHaveAnyValidationErrors();
    }

    // ========================================================================
    // PublishToolkitCommandValidator
    // ========================================================================

    [Fact]
    public void PublishToolkit_ValidCommand_PassesValidation()
    {
        var validator = new PublishToolkitCommandValidator();
        var command = new PublishToolkitCommand(Guid.NewGuid());

        var result = validator.TestValidate(command);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void PublishToolkit_EmptyToolkitId_FailsValidation()
    {
        var validator = new PublishToolkitCommandValidator();
        var command = new PublishToolkitCommand(Guid.Empty);

        var result = validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.ToolkitId);
    }

    // ========================================================================
    // AddDiceToolCommandValidator
    // ========================================================================

    [Fact]
    public void AddDiceTool_ValidCommand_PassesValidation()
    {
        var validator = new AddDiceToolCommandValidator();
        var command = new AddDiceToolCommand(Guid.NewGuid(), "Attack", DiceType.D20, 2, null, true, "#FF0000");

        var result = validator.TestValidate(command);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void AddDiceTool_EmptyToolkitId_FailsValidation()
    {
        var validator = new AddDiceToolCommandValidator();
        var command = new AddDiceToolCommand(Guid.Empty, "Attack", DiceType.D6, 1, null, true, null);

        var result = validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.ToolkitId);
    }

    [Fact]
    public void AddDiceTool_EmptyName_FailsValidation()
    {
        var validator = new AddDiceToolCommandValidator();
        var command = new AddDiceToolCommand(Guid.NewGuid(), "", DiceType.D6, 1, null, true, null);

        var result = validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.Name);
    }

    [Fact]
    public void AddDiceTool_QuantityZero_FailsValidation()
    {
        var validator = new AddDiceToolCommandValidator();
        var command = new AddDiceToolCommand(Guid.NewGuid(), "Dice", DiceType.D6, 0, null, true, null);

        var result = validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.Quantity);
    }

    [Fact]
    public void AddDiceTool_QuantityOver100_FailsValidation()
    {
        var validator = new AddDiceToolCommandValidator();
        var command = new AddDiceToolCommand(Guid.NewGuid(), "Dice", DiceType.D6, 101, null, true, null);

        var result = validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.Quantity);
    }

    [Fact]
    public void AddDiceTool_CustomDiceWithoutFaces_FailsValidation()
    {
        var validator = new AddDiceToolCommandValidator();
        var command = new AddDiceToolCommand(Guid.NewGuid(), "Custom", DiceType.Custom, 1, null, true, null);

        var result = validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.CustomFaces);
    }

    [Fact]
    public void AddDiceTool_CustomDiceWithFaces_PassesValidation()
    {
        var validator = new AddDiceToolCommandValidator();
        var command = new AddDiceToolCommand(Guid.NewGuid(), "Custom", DiceType.Custom, 1, ["Hit", "Miss"], true, null);

        var result = validator.TestValidate(command);

        result.ShouldNotHaveAnyValidationErrors();
    }

    // ========================================================================
    // AddCounterToolCommandValidator
    // ========================================================================

    [Fact]
    public void AddCounterTool_ValidCommand_PassesValidation()
    {
        var validator = new AddCounterToolCommandValidator();
        var command = new AddCounterToolCommand(Guid.NewGuid(), "Health", 0, 100, 50, true, "heart", "#00FF00");

        var result = validator.TestValidate(command);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void AddCounterTool_MinGreaterThanMax_FailsValidation()
    {
        var validator = new AddCounterToolCommandValidator();
        var command = new AddCounterToolCommand(Guid.NewGuid(), "Counter", 100, 50, 75, false, null, null);

        var result = validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.MinValue);
    }

    [Fact]
    public void AddCounterTool_DefaultBelowMin_FailsValidation()
    {
        var validator = new AddCounterToolCommandValidator();
        var command = new AddCounterToolCommand(Guid.NewGuid(), "Counter", 10, 100, 5, false, null, null);

        var result = validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.DefaultValue);
    }

    [Fact]
    public void AddCounterTool_DefaultAboveMax_FailsValidation()
    {
        var validator = new AddCounterToolCommandValidator();
        var command = new AddCounterToolCommand(Guid.NewGuid(), "Counter", 0, 100, 150, false, null, null);

        var result = validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.DefaultValue);
    }

    // ========================================================================
    // RemoveDiceToolCommandValidator
    // ========================================================================

    [Fact]
    public void RemoveDiceTool_ValidCommand_PassesValidation()
    {
        var validator = new RemoveDiceToolCommandValidator();
        var command = new RemoveDiceToolCommand(Guid.NewGuid(), "Attack");

        var result = validator.TestValidate(command);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void RemoveDiceTool_EmptyToolkitId_FailsValidation()
    {
        var validator = new RemoveDiceToolCommandValidator();
        var command = new RemoveDiceToolCommand(Guid.Empty, "Attack");

        var result = validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.ToolkitId);
    }

    [Fact]
    public void RemoveDiceTool_EmptyToolName_FailsValidation()
    {
        var validator = new RemoveDiceToolCommandValidator();
        var command = new RemoveDiceToolCommand(Guid.NewGuid(), "");

        var result = validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.ToolName);
    }

    // ========================================================================
    // RemoveCounterToolCommandValidator
    // ========================================================================

    [Fact]
    public void RemoveCounterTool_ValidCommand_PassesValidation()
    {
        var validator = new RemoveCounterToolCommandValidator();
        var command = new RemoveCounterToolCommand(Guid.NewGuid(), "Health");

        var result = validator.TestValidate(command);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void RemoveCounterTool_EmptyToolkitId_FailsValidation()
    {
        var validator = new RemoveCounterToolCommandValidator();
        var command = new RemoveCounterToolCommand(Guid.Empty, "Health");

        var result = validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.ToolkitId);
    }

    // ========================================================================
    // SetScoringTemplateCommandValidator
    // ========================================================================

    [Fact]
    public void SetScoringTemplate_ValidCommand_PassesValidation()
    {
        var validator = new SetScoringTemplateCommandValidator();
        var command = new SetScoringTemplateCommand(Guid.NewGuid(), ["VP", "Gold"], "VP", ScoreType.Points);

        var result = validator.TestValidate(command);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void SetScoringTemplate_EmptyDimensions_FailsValidation()
    {
        var validator = new SetScoringTemplateCommandValidator();
        var command = new SetScoringTemplateCommand(Guid.NewGuid(), [], "VP", ScoreType.Points);

        var result = validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.Dimensions);
    }

    [Fact]
    public void SetScoringTemplate_EmptyDefaultUnit_FailsValidation()
    {
        var validator = new SetScoringTemplateCommandValidator();
        var command = new SetScoringTemplateCommand(Guid.NewGuid(), ["VP"], "", ScoreType.Points);

        var result = validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.DefaultUnit);
    }

    [Fact]
    public void SetScoringTemplate_DefaultUnitTooLong_FailsValidation()
    {
        var validator = new SetScoringTemplateCommandValidator();
        var command = new SetScoringTemplateCommand(Guid.NewGuid(), ["VP"], new string('X', 51), ScoreType.Points);

        var result = validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.DefaultUnit);
    }

    // ========================================================================
    // SetTurnTemplateCommandValidator
    // ========================================================================

    [Fact]
    public void SetTurnTemplate_ValidCommand_PassesValidation()
    {
        var validator = new SetTurnTemplateCommandValidator();
        var command = new SetTurnTemplateCommand(Guid.NewGuid(), TurnOrderType.RoundRobin, ["Draw", "Play"]);

        var result = validator.TestValidate(command);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void SetTurnTemplate_EmptyToolkitId_FailsValidation()
    {
        var validator = new SetTurnTemplateCommandValidator();
        var command = new SetTurnTemplateCommand(Guid.Empty, TurnOrderType.RoundRobin, null);

        var result = validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.ToolkitId);
    }

    [Fact]
    public void SetTurnTemplate_InvalidTurnOrderType_FailsValidation()
    {
        var validator = new SetTurnTemplateCommandValidator();
        var command = new SetTurnTemplateCommand(Guid.NewGuid(), (TurnOrderType)999, null);

        var result = validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.TurnOrderType);
    }
}
