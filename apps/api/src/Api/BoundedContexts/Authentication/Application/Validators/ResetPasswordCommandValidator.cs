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
        RuleFor(x => x.Token)
            .NotEmpty()
            .WithMessage("Reset token is required")
            .Must(BeValidGuid)
            .WithMessage("Reset token must be a valid GUID");

        RuleFor(x => x.NewPassword)
            .NotEmpty()
            .WithMessage("New password is required")
            .MinimumLength(8)
            .WithMessage("New password must be at least 8 characters")
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

    private static bool BeValidGuid(string token)
    {
        return Guid.TryParse(token, out _);
    }
}
