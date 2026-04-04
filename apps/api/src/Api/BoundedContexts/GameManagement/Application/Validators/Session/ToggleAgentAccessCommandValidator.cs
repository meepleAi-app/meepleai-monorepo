using Api.BoundedContexts.GameManagement.Application.Commands.Session;
using FluentValidation;

namespace Api.BoundedContexts.GameManagement.Application.Validators.Session;

/// <summary>
/// Validator for ToggleAgentAccessCommand.
/// Ensures all GUID properties are non-empty.
/// </summary>
internal sealed class ToggleAgentAccessCommandValidator : AbstractValidator<ToggleAgentAccessCommand>
{
    public ToggleAgentAccessCommandValidator()
    {
        RuleFor(x => x.SessionId)
            .NotEmpty().WithMessage("Session ID is required");

        RuleFor(x => x.ParticipantId)
            .NotEmpty().WithMessage("Participant ID is required");

        RuleFor(x => x.RequestingUserId)
            .NotEmpty().WithMessage("Requesting user ID is required");
    }
}
