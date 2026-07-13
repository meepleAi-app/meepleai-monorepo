using Api.BoundedContexts.Authentication.Application.Commands.PasswordReset;
using FluentValidation;

namespace Api.BoundedContexts.Authentication.Application.Validators;

/// <summary>
/// Validator for ResetPasswordCommand.
/// Ensures reset token is a valid GUID and new password meets complexity requirements.
/// Issue #1449: FluentValidation for Authentication CQRS pipeline
/// </summary>
internal sealed class ResetPasswordCommandValidator : AbstractValidator<ResetPasswordCommand>
{
    public ResetPasswordCommandValidator()
    {
        // Issue #2806: the reset token is a 32-byte secure random value encoded as
        // base64url by PasswordResetService — NOT a GUID. Only presence is enforced
        // here; existence, single-use and expiry are checked in
        // PasswordResetService.ResetPasswordAsync. The previous .Must(BeValidGuid)
        // rejected every real token (HTTP 422), breaking password reset for all users.
        RuleFor(x => x.Token)
            .NotEmpty()
            .WithMessage("Reset token is required");

        // I7 (auth security fixes): the new password must satisfy the
        // 12-char minimum enforced by PasswordHash.Create.
        RuleFor(x => x.NewPassword)
            .NotEmpty()
            .WithMessage("New password is required")
            .MinimumLength(12)
            .WithMessage("New password must be at least 12 characters")
            .MaximumLength(128)
            .WithMessage("New password must not exceed 128 characters")
            .Matches(@"[A-Z]")
            .WithMessage("New password must contain at least one uppercase letter")
            .Matches(@"[a-z]")
            .WithMessage("New password must contain at least one lowercase letter")
            .Matches(@"[0-9]")
            .WithMessage("New password must contain at least one digit")
            .Matches(@"[^a-zA-Z0-9]")
            .WithMessage("New password must contain at least one special character");
    }
}
