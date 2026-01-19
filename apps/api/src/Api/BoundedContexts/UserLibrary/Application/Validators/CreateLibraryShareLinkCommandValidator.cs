using Api.BoundedContexts.UserLibrary.Application.Commands;
using FluentValidation;

namespace Api.BoundedContexts.UserLibrary.Application.Validators;

/// <summary>
/// Validator for CreateLibraryShareLinkCommand.
/// </summary>
internal sealed class CreateLibraryShareLinkCommandValidator : AbstractValidator<CreateLibraryShareLinkCommand>
{
    private static readonly string[] ValidPrivacyLevels = { "public", "unlisted" };

    public CreateLibraryShareLinkCommandValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty()
            .WithMessage("UserId is required");

        RuleFor(x => x.PrivacyLevel)
            .NotEmpty()
            .WithMessage("PrivacyLevel is required")
            .Must(level => ValidPrivacyLevels.Contains(level, StringComparer.OrdinalIgnoreCase))
            .WithMessage("PrivacyLevel must be 'public' or 'unlisted'");

        RuleFor(x => x.ExpiresAt)
            .Must(expiresAt => !expiresAt.HasValue || expiresAt.Value > DateTime.UtcNow)
            .WithMessage("ExpiresAt must be in the future")
            .When(x => x.ExpiresAt.HasValue);
    }
}
