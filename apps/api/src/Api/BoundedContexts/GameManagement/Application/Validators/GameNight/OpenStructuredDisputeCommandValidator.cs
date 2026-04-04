using Api.BoundedContexts.GameManagement.Application.Commands.GameNight;
using FluentValidation;

namespace Api.BoundedContexts.GameManagement.Application.Validators.GameNight;

/// <summary>
/// Validates <see cref="OpenStructuredDisputeCommand"/> inputs.
/// </summary>
internal sealed class OpenStructuredDisputeCommandValidator : AbstractValidator<OpenStructuredDisputeCommand>
{
    public OpenStructuredDisputeCommandValidator()
    {
        RuleFor(x => x.SessionId)
            .NotEmpty()
            .WithMessage("Session ID is required");

        RuleFor(x => x.InitiatorPlayerId)
            .NotEmpty()
            .WithMessage("Initiator player ID is required");

        RuleFor(x => x.InitiatorClaim)
            .NotEmpty()
            .WithMessage("Initiator claim is required")
            .MaximumLength(2000)
            .WithMessage("Initiator claim cannot exceed 2000 characters");
    }
}
