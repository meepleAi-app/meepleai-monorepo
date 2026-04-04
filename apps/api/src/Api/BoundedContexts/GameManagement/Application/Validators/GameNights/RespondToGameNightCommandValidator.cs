using Api.BoundedContexts.GameManagement.Application.Commands.GameNights;
using Api.BoundedContexts.GameManagement.Domain.Enums;
using FluentValidation;

namespace Api.BoundedContexts.GameManagement.Application.Validators.GameNights;

/// <summary>
/// Validator for RespondToGameNightCommand.
/// Issue #43: Game Night CRUD validators.
/// </summary>
internal sealed class RespondToGameNightCommandValidator : AbstractValidator<RespondToGameNightCommand>
{
    public RespondToGameNightCommandValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty()
            .WithMessage("User ID is required");

        RuleFor(x => x.GameNightId)
            .NotEmpty()
            .WithMessage("Game night ID is required");

        RuleFor(x => x.Response)
            .IsInEnum()
            .WithMessage("Invalid RSVP response value")
            .Must(r => r != RsvpStatus.Pending)
            .WithMessage("Cannot set RSVP status to Pending");
    }
}
