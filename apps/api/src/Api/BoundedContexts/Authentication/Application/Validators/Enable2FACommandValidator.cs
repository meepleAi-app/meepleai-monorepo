using Api.BoundedContexts.Authentication.Application.Commands;
using FluentValidation;

namespace Api.BoundedContexts.Authentication.Application.Validators;

/// <summary>
/// Validator for Enable2FACommand.
/// Ensures user ID is valid and TOTP code is a 6-digit numeric code.
/// Issue #1449: FluentValidation for Authentication CQRS pipeline
/// </summary>
internal sealed class Enable2FACommandValidator : AbstractValidator<Enable2FACommand>
{
    public Enable2FACommandValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty()
            .WithMessage("User ID is required");

        RuleFor(x => x.TotpCode)
            .NotEmpty()
            .WithMessage("TOTP code is required")
            .Length(6)
            .WithMessage("TOTP code must be exactly 6 digits")
            .Matches(@"^\d{6}$")
            .WithMessage("TOTP code must contain only digits");
    }
}
