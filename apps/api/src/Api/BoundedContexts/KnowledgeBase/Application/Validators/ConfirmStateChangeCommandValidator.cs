using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using FluentValidation;

namespace Api.BoundedContexts.KnowledgeBase.Application.Validators;

/// <summary>
/// Validator for ConfirmStateChangeCommand.
/// Issue #2405 - Ledger Mode state tracking
/// </summary>
internal sealed class ConfirmStateChangeCommandValidator : AbstractValidator<ConfirmStateChangeCommand>
{
    public ConfirmStateChangeCommandValidator()
    {
        RuleFor(x => x.SessionId)
            .NotEmpty()
            .WithMessage("Session ID is required");

        RuleFor(x => x.StateChanges)
            .NotNull()
            .WithMessage("State changes cannot be null")
            .NotEmpty()
            .WithMessage("State changes cannot be empty");

        RuleFor(x => x.UserId)
            .NotEmpty()
            .WithMessage("User ID is required");

        RuleFor(x => x.Description)
            .MaximumLength(500)
            .WithMessage("Description cannot exceed 500 characters")
            .When(x => !string.IsNullOrWhiteSpace(x.Description));
    }
}
