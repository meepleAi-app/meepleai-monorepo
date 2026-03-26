using Api.BoundedContexts.Authentication.Application.Commands;
using FluentValidation;

namespace Api.BoundedContexts.Authentication.Application.Validators;

/// <summary>
/// Validator for UpdatePreferencesCommand.
/// Ensures user ID, language, theme, and data retention days are valid.
/// </summary>
internal sealed class UpdatePreferencesCommandValidator : AbstractValidator<UpdatePreferencesCommand>
{
    public UpdatePreferencesCommandValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty()
            .WithMessage("User ID is required");

        RuleFor(x => x.Language)
            .NotEmpty()
            .WithMessage("Language is required")
            .MaximumLength(10)
            .WithMessage("Language code must not exceed 10 characters");

        RuleFor(x => x.Theme)
            .NotEmpty()
            .WithMessage("Theme is required")
            .MaximumLength(20)
            .WithMessage("Theme must not exceed 20 characters");

        RuleFor(x => x.DataRetentionDays)
            .InclusiveBetween(1, 3650)
            .WithMessage("Data retention must be between 1 and 3650 days");
    }
}
