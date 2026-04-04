using Api.BoundedContexts.Authentication.Application.Commands;
using FluentValidation;

namespace Api.BoundedContexts.Authentication.Application.Validators;

/// <summary>
/// Validator for Disable2FACommand.
/// Ensures user ID, password, and TOTP/backup code are provided.
/// </summary>
internal sealed class Disable2FACommandValidator : AbstractValidator<Disable2FACommand>
{
    public Disable2FACommandValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty()
            .WithMessage("User ID is required");

        RuleFor(x => x.CurrentPassword)
            .NotEmpty()
            .WithMessage("Current password is required")
            .MinimumLength(8)
            .WithMessage("Current password must be at least 8 characters");

        RuleFor(x => x.TotpOrBackupCode)
            .NotEmpty()
            .WithMessage("TOTP or backup code is required")
            .MaximumLength(20)
            .WithMessage("Code must not exceed 20 characters");
    }
}
