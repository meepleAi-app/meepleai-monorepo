using Api.BoundedContexts.GameManagement.Application.Commands.LiveSessions;
using FluentValidation;

namespace Api.BoundedContexts.GameManagement.Application.Validators.LiveSessions;

/// <summary>
/// Validator for SaveCompleteSessionStateCommand.
/// Ensures SessionId is a non-empty GUID.
/// </summary>
internal sealed class SaveCompleteSessionStateCommandValidator : AbstractValidator<SaveCompleteSessionStateCommand>
{
    public SaveCompleteSessionStateCommandValidator()
    {
        RuleFor(x => x.SessionId)
            .NotEmpty().WithMessage("Session ID is required");
    }
}
