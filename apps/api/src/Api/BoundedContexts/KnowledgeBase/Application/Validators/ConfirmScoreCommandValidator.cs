using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using FluentValidation;

namespace Api.BoundedContexts.KnowledgeBase.Application.Validators;

/// <summary>
/// Validator for ConfirmScoreCommand.
/// </summary>
internal sealed class ConfirmScoreCommandValidator : AbstractValidator<ConfirmScoreCommand>
{
    public ConfirmScoreCommandValidator()
    {
        RuleFor(x => x.SessionId)
            .NotEmpty()
            .WithMessage("Session ID is required");

        RuleFor(x => x.PlayerId)
            .NotEmpty()
            .WithMessage("Player ID is required");

        RuleFor(x => x.Dimension)
            .NotEmpty()
            .WithMessage("Dimension is required")
            .MaximumLength(50)
            .WithMessage("Dimension must not exceed 50 characters");

        RuleFor(x => x.Round)
            .GreaterThan(0)
            .WithMessage("Round must be at least 1");
    }
}
