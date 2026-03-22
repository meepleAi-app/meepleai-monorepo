using Api.BoundedContexts.GameManagement.Application.Commands.GameNights;
using FluentValidation;

namespace Api.BoundedContexts.GameManagement.Application.Validators.GameNights;

/// <summary>
/// Validator for PublishGameNightCommand.
/// Ensures GameNightId and UserId are non-empty GUIDs.
/// </summary>
internal sealed class PublishGameNightCommandValidator : AbstractValidator<PublishGameNightCommand>
{
    public PublishGameNightCommandValidator()
    {
        RuleFor(x => x.GameNightId)
            .NotEmpty().WithMessage("Game night ID is required");

        RuleFor(x => x.UserId)
            .NotEmpty().WithMessage("User ID is required");
    }
}
