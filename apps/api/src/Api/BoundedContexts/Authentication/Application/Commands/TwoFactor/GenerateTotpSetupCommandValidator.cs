using FluentValidation;

namespace Api.BoundedContexts.Authentication.Application.Commands.TwoFactor;

/// <summary>
/// Validates GenerateTotpSetupCommand.
/// Ensures user ID and email are provided for TOTP enrollment.
/// </summary>
internal sealed class GenerateTotpSetupCommandValidator : AbstractValidator<GenerateTotpSetupCommand>
{
    public GenerateTotpSetupCommandValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty()
            .WithMessage("User ID is required");

        RuleFor(x => x.UserEmail)
            .NotEmpty()
            .WithMessage("User email is required")
            .EmailAddress()
            .WithMessage("User email must be a valid email address")
            .MaximumLength(255)
            .WithMessage("User email must not exceed 255 characters");
    }
}
