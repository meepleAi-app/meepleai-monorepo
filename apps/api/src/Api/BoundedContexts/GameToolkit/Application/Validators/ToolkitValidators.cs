#pragma warning disable MA0048 // File name must match type name - Contains related validators
using Api.BoundedContexts.GameToolkit.Application.Commands;
using Api.BoundedContexts.GameToolkit.Domain.Enums;
using FluentValidation;

namespace Api.BoundedContexts.GameToolkit.Application.Validators;

internal class CreateToolkitCommandValidator : AbstractValidator<CreateToolkitCommand>
{
    public CreateToolkitCommandValidator()
    {
        RuleFor(x => x.GameId).Must(id => id.HasValue && id.Value != Guid.Empty).WithMessage("GameId is required");
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200).WithMessage("Name is required (max 200 chars)");
        RuleFor(x => x.CreatedByUserId).NotEmpty().WithMessage("CreatedByUserId is required");
    }
}

internal class UpdateToolkitCommandValidator : AbstractValidator<UpdateToolkitCommand>
{
    public UpdateToolkitCommandValidator()
    {
        RuleFor(x => x.ToolkitId).NotEmpty().WithMessage("ToolkitId is required");
        RuleFor(x => x.Name).MaximumLength(200).When(x => x.Name != null)
            .WithMessage("Name cannot exceed 200 characters");
    }
}

internal class PublishToolkitCommandValidator : AbstractValidator<PublishToolkitCommand>
{
    public PublishToolkitCommandValidator()
    {
        RuleFor(x => x.ToolkitId).NotEmpty().WithMessage("ToolkitId is required");
    }
}

internal class AddDiceToolCommandValidator : AbstractValidator<AddDiceToolCommand>
{
    public AddDiceToolCommandValidator()
    {
        RuleFor(x => x.ToolkitId).NotEmpty().WithMessage("ToolkitId is required");
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100).WithMessage("Name is required (max 100 chars)");
        RuleFor(x => x.DiceType).IsInEnum().WithMessage("Invalid DiceType");
        RuleFor(x => x.Quantity).InclusiveBetween(1, 100).WithMessage("Quantity must be between 1 and 100");
        RuleFor(x => x.CustomFaces).NotEmpty()
            .When(x => x.DiceType == DiceType.Custom)
            .WithMessage("Custom dice must have faces defined");
    }
}

internal class AddCardToolCommandValidator : AbstractValidator<AddCardToolCommand>
{
    public AddCardToolCommandValidator()
    {
        RuleFor(x => x.ToolkitId).NotEmpty().WithMessage("ToolkitId is required");
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100).WithMessage("Name is required (max 100 chars)");
        RuleFor(x => x.DeckType).NotEmpty().MaximumLength(100).WithMessage("DeckType is required (max 100 chars)");
        RuleFor(x => x.CardCount).InclusiveBetween(1, 1000).WithMessage("CardCount must be between 1 and 1000");
        RuleFor(x => x.CardEntries)
            .Must(entries => entries == null || entries.Count <= 1000)
            .WithMessage("CardEntries count cannot exceed 1000");
        RuleFor(x => x.DefaultZone).IsInEnum().WithMessage("Invalid CardZone");
        RuleFor(x => x.DefaultOrientation).IsInEnum().WithMessage("Invalid CardOrientation");
    }
}

internal class AddTimerToolCommandValidator : AbstractValidator<AddTimerToolCommand>
{
    public AddTimerToolCommandValidator()
    {
        RuleFor(x => x.ToolkitId).NotEmpty().WithMessage("ToolkitId is required");
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100).WithMessage("Name is required (max 100 chars)");
        RuleFor(x => x.DurationSeconds).InclusiveBetween(1, 86400)
            .WithMessage("Duration must be between 1 second and 24 hours");
        RuleFor(x => x.TimerType).IsInEnum().WithMessage("Invalid TimerType");
        RuleFor(x => x.WarningThresholdSeconds)
            .GreaterThan(0).When(x => x.WarningThresholdSeconds.HasValue)
            .WithMessage("Warning threshold must be positive");
        RuleFor(x => x.WarningThresholdSeconds)
            .LessThan(x => x.DurationSeconds).When(x => x.WarningThresholdSeconds.HasValue)
            .WithMessage("Warning threshold must be less than duration");
        RuleFor(x => x.IsPerPlayer).Equal(true)
            .When(x => x.TimerType == TimerType.Chess)
            .WithMessage("Chess timer must be per-player");
    }
}

internal class AddCounterToolCommandValidator : AbstractValidator<AddCounterToolCommand>
{
    public AddCounterToolCommandValidator()
    {
        RuleFor(x => x.ToolkitId).NotEmpty().WithMessage("ToolkitId is required");
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100).WithMessage("Name is required (max 100 chars)");
        RuleFor(x => x.MinValue).LessThanOrEqualTo(x => x.MaxValue)
            .WithMessage("MinValue cannot be greater than MaxValue");
        RuleFor(x => x.DefaultValue)
            .GreaterThanOrEqualTo(x => x.MinValue)
            .LessThanOrEqualTo(x => x.MaxValue)
            .WithMessage("DefaultValue must be between MinValue and MaxValue");
    }
}

internal class RemoveDiceToolCommandValidator : AbstractValidator<RemoveDiceToolCommand>
{
    public RemoveDiceToolCommandValidator()
    {
        RuleFor(x => x.ToolkitId).NotEmpty().WithMessage("ToolkitId is required");
        RuleFor(x => x.ToolName).NotEmpty().WithMessage("ToolName is required");
    }
}

internal class RemoveCardToolCommandValidator : AbstractValidator<RemoveCardToolCommand>
{
    public RemoveCardToolCommandValidator()
    {
        RuleFor(x => x.ToolkitId).NotEmpty().WithMessage("ToolkitId is required");
        RuleFor(x => x.ToolName).NotEmpty().WithMessage("ToolName is required");
    }
}

internal class RemoveTimerToolCommandValidator : AbstractValidator<RemoveTimerToolCommand>
{
    public RemoveTimerToolCommandValidator()
    {
        RuleFor(x => x.ToolkitId).NotEmpty().WithMessage("ToolkitId is required");
        RuleFor(x => x.ToolName).NotEmpty().WithMessage("ToolName is required");
    }
}

internal class RemoveCounterToolCommandValidator : AbstractValidator<RemoveCounterToolCommand>
{
    public RemoveCounterToolCommandValidator()
    {
        RuleFor(x => x.ToolkitId).NotEmpty().WithMessage("ToolkitId is required");
        RuleFor(x => x.ToolName).NotEmpty().WithMessage("ToolName is required");
    }
}

internal class SetScoringTemplateCommandValidator : AbstractValidator<SetScoringTemplateCommand>
{
    public SetScoringTemplateCommandValidator()
    {
        RuleFor(x => x.ToolkitId).NotEmpty().WithMessage("ToolkitId is required");
        RuleFor(x => x.Dimensions).NotEmpty().WithMessage("At least one scoring dimension is required");
        RuleFor(x => x.DefaultUnit).NotEmpty().MaximumLength(50).WithMessage("DefaultUnit is required");
        RuleFor(x => x.ScoreType).IsInEnum().WithMessage("Invalid ScoreType");
    }
}

internal class SetTurnTemplateCommandValidator : AbstractValidator<SetTurnTemplateCommand>
{
    public SetTurnTemplateCommandValidator()
    {
        RuleFor(x => x.ToolkitId).NotEmpty().WithMessage("ToolkitId is required");
        RuleFor(x => x.TurnOrderType).IsInEnum().WithMessage("Invalid TurnOrderType");
    }
}

internal class SetStateTemplateCommandValidator : AbstractValidator<SetStateTemplateCommand>
{
    public SetStateTemplateCommandValidator()
    {
        RuleFor(x => x.ToolkitId).NotEmpty().WithMessage("ToolkitId is required");
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200).WithMessage("Template name is required (max 200 chars)");
        RuleFor(x => x.Category).IsInEnum().WithMessage("Invalid TemplateCategory");
        RuleFor(x => x.SchemaJson).NotEmpty().WithMessage("Schema JSON is required");
    }
}

internal class ClearStateTemplateCommandValidator : AbstractValidator<ClearStateTemplateCommand>
{
    public ClearStateTemplateCommandValidator()
    {
        RuleFor(x => x.ToolkitId).NotEmpty().WithMessage("ToolkitId is required");
    }
}
