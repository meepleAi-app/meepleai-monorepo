using Api.BoundedContexts.GameManagement.Application.Commands;
using FluentValidation;

namespace Api.BoundedContexts.GameManagement.Application.Validators;

/// <summary>
/// Validator for SuggestMoveCommand.
/// Ensures SessionId, AgentId, and UserId are non-empty GUIDs.
/// </summary>
internal sealed class SuggestMoveCommandValidator : AbstractValidator<SuggestMoveCommand>
{
    public SuggestMoveCommandValidator()
    {
        RuleFor(x => x.SessionId)
            .NotEmpty().WithMessage("Session ID is required");

        RuleFor(x => x.AgentId)
            .NotEmpty().WithMessage("Agent ID is required");

        RuleFor(x => x.Query)
            .MaximumLength(2000).WithMessage("Query must not exceed 2000 characters")
            .When(x => x.Query is not null);

        RuleFor(x => x.UserId)
            .NotEmpty().WithMessage("User ID is required");
    }
}
