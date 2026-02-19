#pragma warning disable MA0048 // File name must match type name - Contains related validators
using Api.BoundedContexts.GameToolkit.Application.Commands;
using Api.BoundedContexts.GameToolkit.Domain.Enums;
using FluentValidation;

namespace Api.BoundedContexts.GameToolkit.Application.Validators;

internal class CreateToolkitCommandValidator : AbstractValidator<CreateToolkitCommand>
{
    public CreateToolkitCommandValidator()
    {
        RuleFor(x => x.GameId).NotEmpty().WithMessage("GameId is required");
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
