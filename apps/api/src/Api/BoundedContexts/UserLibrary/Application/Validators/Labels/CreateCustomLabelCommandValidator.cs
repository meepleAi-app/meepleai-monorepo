using Api.BoundedContexts.UserLibrary.Application.Commands.Labels;
using Api.BoundedContexts.UserLibrary.Domain.Entities;
using FluentValidation;

namespace Api.BoundedContexts.UserLibrary.Application.Validators.Labels;

/// <summary>
/// Validator for CreateCustomLabelCommand.
/// </summary>
internal sealed class CreateCustomLabelCommandValidator : AbstractValidator<CreateCustomLabelCommand>
{
    public CreateCustomLabelCommandValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty()
            .WithMessage("UserId is required");

        RuleFor(x => x.Name)
            .NotEmpty()
            .WithMessage("Label name is required")
            .MaximumLength(GameLabel.MaxNameLength)
            .WithMessage($"Label name cannot exceed {GameLabel.MaxNameLength} characters");

        RuleFor(x => x.Color)
            .NotEmpty()
            .WithMessage("Label color is required")
            .MaximumLength(GameLabel.MaxColorLength)
            .WithMessage($"Label color cannot exceed {GameLabel.MaxColorLength} characters")
            .Matches(@"^#[0-9A-Fa-f]{6}$")
            .WithMessage("Label color must be a valid hex color (e.g., #22c55e)");
    }
}
