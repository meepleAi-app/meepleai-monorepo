using Api.BoundedContexts.GameManagement.Application.Commands;
using FluentValidation;

namespace Api.BoundedContexts.GameManagement.Application.Validators;

/// <summary>
/// Validator for AcquireEditorLockCommand.
/// Ensures GameId and UserId are non-empty GUIDs and UserEmail is provided.
/// </summary>
internal sealed class AcquireEditorLockCommandValidator : AbstractValidator<AcquireEditorLockCommand>
{
    public AcquireEditorLockCommandValidator()
    {
        RuleFor(x => x.GameId)
            .NotEmpty().WithMessage("Game ID is required");

        RuleFor(x => x.UserId)
            .NotEmpty().WithMessage("User ID is required");

        RuleFor(x => x.UserEmail)
            .NotEmpty().WithMessage("User email is required")
            .MaximumLength(320).WithMessage("User email must not exceed 320 characters");
    }
}
