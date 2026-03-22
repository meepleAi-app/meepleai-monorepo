using Api.BoundedContexts.GameManagement.Application.Commands.GameNights;
using FluentValidation;

namespace Api.BoundedContexts.GameManagement.Application.Validators.GameNights;

/// <summary>
/// Validator for CancelGameNightCommand.
/// Ensures GameNightId and UserId are non-empty GUIDs.
/// </summary>
internal sealed class CancelGameNightCommandValidator : AbstractValidator<CancelGameNightCommand>
{
    public CancelGameNightCommandValidator()
    {
        RuleFor(x => x.GameNightId)
            .NotEmpty().WithMessage("Game night ID is required");

        RuleFor(x => x.UserId)
            .NotEmpty().WithMessage("User ID is required");
    }
}
