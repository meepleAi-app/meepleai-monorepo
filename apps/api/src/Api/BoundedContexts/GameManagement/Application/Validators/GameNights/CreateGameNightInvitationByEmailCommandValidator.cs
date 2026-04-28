using Api.BoundedContexts.GameManagement.Application.Commands.GameNights;
using FluentValidation;

namespace Api.BoundedContexts.GameManagement.Application.Validators.GameNights;

/// <summary>
/// Validator for <see cref="CreateGameNightInvitationByEmailCommand"/>.
/// Issue #607 (Wave A.5a): GameNight token-based RSVP backend extension.
/// </summary>
internal sealed class CreateGameNightInvitationByEmailCommandValidator
    : AbstractValidator<CreateGameNightInvitationByEmailCommand>
{
    public CreateGameNightInvitationByEmailCommandValidator()
    {
        RuleFor(x => x.GameNightId)
            .NotEmpty().WithMessage("Game night ID is required");

        RuleFor(x => x.OrganizerUserId)
            .NotEmpty().WithMessage("Organizer user ID is required");

        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required")
            .EmailAddress().WithMessage("Email must be a valid address")
            .MaximumLength(254).WithMessage("Email exceeds the 254-character RFC limit");
    }
}
