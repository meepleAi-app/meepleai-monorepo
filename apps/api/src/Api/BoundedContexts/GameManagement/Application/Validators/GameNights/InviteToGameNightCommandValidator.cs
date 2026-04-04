using Api.BoundedContexts.GameManagement.Application.Commands.GameNights;
using FluentValidation;

namespace Api.BoundedContexts.GameManagement.Application.Validators.GameNights;

/// <summary>
/// Validator for InviteToGameNightCommand.
/// Ensures all GUID properties are non-empty and invite list is valid.
/// </summary>
internal sealed class InviteToGameNightCommandValidator : AbstractValidator<InviteToGameNightCommand>
{
    public InviteToGameNightCommandValidator()
    {
        RuleFor(x => x.GameNightId)
            .NotEmpty().WithMessage("Game night ID is required");

        RuleFor(x => x.UserId)
            .NotEmpty().WithMessage("User ID is required");

        RuleFor(x => x.InvitedUserIds)
            .NotNull().WithMessage("Invited user IDs list is required")
            .Must(ids => ids.Count > 0).WithMessage("At least one invited user ID is required");

        RuleForEach(x => x.InvitedUserIds)
            .NotEmpty().WithMessage("Invited user ID must not be empty");
    }
}
