using Api.BoundedContexts.Authentication.Application.Commands;
using FluentValidation;

namespace Api.BoundedContexts.Authentication.Application.Validators;

/// <summary>
/// Validator for UpdatePreferencesCommand.
/// Ensures user ID, language, theme, and data retention days are valid.
/// #4157: ogni campo e` opzionale (null = invariato); un campo presente si valida come prima, quindi
/// un <c>dataRetentionDays: 0</c> esplicito risponde ancora 422.
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
            .WithMessage("Language code must not exceed 10 characters")
            .When(x => x.Language is not null);

        RuleFor(x => x.Theme)
            .NotEmpty()
            .WithMessage("Theme is required")
            .MaximumLength(20)
            .WithMessage("Theme must not exceed 20 characters")
            .When(x => x.Theme is not null);

        RuleFor(x => x.DataRetentionDays)
            .InclusiveBetween(1, 3650)
            .WithMessage("Data retention must be between 1 and 3650 days")
            .When(x => x.DataRetentionDays.HasValue);
    }
}
