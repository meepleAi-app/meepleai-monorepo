using Api.BoundedContexts.GameManagement.Application.Commands;
using FluentValidation;

namespace Api.BoundedContexts.GameManagement.Application.Validators;

/// <summary>
/// Validator for ApplySuggestionCommand.
/// Ensures SessionId, SuggestionId, and UserId are non-empty GUIDs and StateChanges is provided.
/// </summary>
internal sealed class ApplySuggestionCommandValidator : AbstractValidator<ApplySuggestionCommand>
{
    public ApplySuggestionCommandValidator()
    {
        RuleFor(x => x.SessionId)
            .NotEmpty().WithMessage("Session ID is required");

        RuleFor(x => x.SuggestionId)
            .NotEmpty().WithMessage("Suggestion ID is required");

        RuleFor(x => x.StateChanges)
            .NotNull().WithMessage("State changes are required");

        RuleFor(x => x.UserId)
            .NotEmpty().WithMessage("User ID is required");
    }
}
