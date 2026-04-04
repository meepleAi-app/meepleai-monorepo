using Api.BoundedContexts.Administration.Application.Commands;
using FluentValidation;

namespace Api.BoundedContexts.Administration.Application.Validators;

/// <summary>
/// Validator for UpdateUserAiConsentCommand.
/// Ensures user ID and consent version are valid.
/// </summary>
internal sealed class UpdateUserAiConsentCommandValidator : AbstractValidator<UpdateUserAiConsentCommand>
{
    public UpdateUserAiConsentCommandValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty()
            .WithMessage("UserId is required");

        RuleFor(x => x.ConsentVersion)
            .NotEmpty()
            .WithMessage("ConsentVersion is required")
            .MaximumLength(50)
            .WithMessage("ConsentVersion must not exceed 50 characters");
    }
}
