using FluentValidation;

namespace Api.BoundedContexts.Authentication.Application.Commands.UserProfile;

/// <summary>
/// Validator for SaveUserInterestsCommand.
/// Issue #124: Invitation system onboarding wizard.
/// </summary>
internal sealed class SaveUserInterestsCommandValidator : AbstractValidator<SaveUserInterestsCommand>
{
    private static readonly string[] AllowedInterests =
    {
        "Strategy", "Party", "Cooperative", "Family", "Thematic",
        "Abstract", "Card", "Dice", "Miniatures"
    };

    public SaveUserInterestsCommandValidator()
    {
        RuleFor(x => x.UserId)
            .NotEqual(Guid.Empty)
            .WithMessage("UserId is required");

        RuleFor(x => x.Interests)
            .NotNull()
            .WithMessage("Interests list is required");

        RuleForEach(x => x.Interests)
            .Must(interest => AllowedInterests.Contains(interest, StringComparer.OrdinalIgnoreCase))
            .WithMessage(interest => $"Interest '{{PropertyValue}}' is not valid. Allowed: {string.Join(", ", AllowedInterests)}");
    }
}
