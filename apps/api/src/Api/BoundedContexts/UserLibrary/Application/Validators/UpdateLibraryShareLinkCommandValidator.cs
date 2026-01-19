using Api.BoundedContexts.UserLibrary.Application.Commands;
using FluentValidation;

namespace Api.BoundedContexts.UserLibrary.Application.Validators;

/// <summary>
/// Validator for UpdateLibraryShareLinkCommand.
/// </summary>
internal sealed class UpdateLibraryShareLinkCommandValidator : AbstractValidator<UpdateLibraryShareLinkCommand>
{
    private static readonly string[] ValidPrivacyLevels = { "public", "unlisted" };

    public UpdateLibraryShareLinkCommandValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty()
            .WithMessage("UserId is required");

        RuleFor(x => x.ShareToken)
            .NotEmpty()
            .WithMessage("ShareToken is required")
            .Length(32)
            .WithMessage("ShareToken must be exactly 32 characters");

        RuleFor(x => x.PrivacyLevel)
            .Must(level => string.IsNullOrEmpty(level) || ValidPrivacyLevels.Contains(level, StringComparer.OrdinalIgnoreCase))
            .WithMessage("PrivacyLevel must be 'public' or 'unlisted'")
            .When(x => !string.IsNullOrEmpty(x.PrivacyLevel));

        RuleFor(x => x.ExpiresAt)
            .Must(expiresAt => !expiresAt.HasValue || expiresAt.Value > DateTime.UtcNow)
            .WithMessage("ExpiresAt must be in the future")
            .When(x => x.ExpiresAt.HasValue);
    }
}
