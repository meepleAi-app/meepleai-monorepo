using Api.BoundedContexts.Authentication.Application.Commands;
using FluentValidation;

namespace Api.BoundedContexts.Authentication.Application.Validators;

/// <summary>
/// Validator for UpdateUserProfileCommand.
/// Ensures user ID is provided and optional fields have proper format and length.
/// </summary>
internal sealed class UpdateUserProfileCommandValidator : AbstractValidator<UpdateUserProfileCommand>
{
    public UpdateUserProfileCommandValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty()
            .WithMessage("User ID is required");

        RuleFor(x => x.DisplayName)
            .MinimumLength(2)
            .WithMessage("Display name must be at least 2 characters")
            .MaximumLength(100)
            .WithMessage("Display name must not exceed 100 characters")
            .Matches(@"^[a-zA-Z0-9\s\-_\.]+$")
            .WithMessage("Display name can only contain letters, numbers, spaces, hyphens, underscores, and periods")
            .When(x => x.DisplayName != null);

        RuleFor(x => x.Email)
            .EmailAddress()
            .WithMessage("Email must be a valid email address")
            .MaximumLength(255)
            .WithMessage("Email must not exceed 255 characters")
            .When(x => x.Email != null);

        RuleFor(x => x.AvatarUrl)
            .MaximumLength(2048)
            .WithMessage("Avatar URL must not exceed 2048 characters")
            .Must(url => Uri.TryCreate(url, UriKind.Absolute, out _))
            .WithMessage("Avatar URL must be a valid absolute URL")
            .When(x => x.AvatarUrl != null);

        RuleFor(x => x.Bio)
            .MaximumLength(500)
            .WithMessage("Bio must not exceed 500 characters")
            .When(x => x.Bio != null);
    }
}
