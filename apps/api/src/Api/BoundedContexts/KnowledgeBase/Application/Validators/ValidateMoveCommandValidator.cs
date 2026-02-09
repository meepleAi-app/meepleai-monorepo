using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using FluentValidation;

namespace Api.BoundedContexts.KnowledgeBase.Application.Validators;

/// <summary>
/// Validator for ValidateMoveCommand.
/// Issue #3760: Arbitro Agent Move Validation Logic.
/// </summary>
internal sealed class ValidateMoveCommandValidator : AbstractValidator<ValidateMoveCommand>
{
    public ValidateMoveCommandValidator()
    {
        RuleFor(x => x.GameSessionId)
            .NotEmpty().WithMessage("GameSessionId is required");

        RuleFor(x => x.PlayerName)
            .NotEmpty().WithMessage("PlayerName is required")
            .MaximumLength(100).WithMessage("PlayerName must not exceed 100 characters");

        RuleFor(x => x.Action)
            .NotEmpty().WithMessage("Action is required")
            .MaximumLength(200).WithMessage("Action must not exceed 200 characters");

        RuleFor(x => x.Position)
            .MaximumLength(50).WithMessage("Position must not exceed 50 characters")
            .When(x => !string.IsNullOrWhiteSpace(x.Position));

        RuleFor(x => x.AdditionalContext)
            .Must(ctx => ctx == null || ctx.Count <= 20)
            .WithMessage("AdditionalContext must not exceed 20 key-value pairs")
            .When(x => x.AdditionalContext != null);
    }
}
