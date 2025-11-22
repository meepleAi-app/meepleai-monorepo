using Api.BoundedContexts.Authentication.Application.Commands;
using FluentValidation;

namespace Api.BoundedContexts.Authentication.Application.Validators;

/// <summary>
/// Validator for ChangePasswordCommand.
/// Ensures current password is provided, new password meets complexity requirements,
/// and new password is different from current password.
/// Issue #1449: FluentValidation for Authentication CQRS pipeline
/// </summary>
public sealed class ChangePasswordCommandValidator : AbstractValidator<ChangePasswordCommand>
{
    public ChangePasswordCommandValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty()
            .WithMessage("User ID is required");

        RuleFor(x => x.CurrentPassword)
            .NotEmpty()
            .WithMessage("Current password is required")
            .MinimumLength(8)
            .WithMessage("Current password must be at least 8 characters");

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

        RuleFor(x => x)
            .Must(cmd => cmd.NewPassword != cmd.CurrentPassword)
            .WithMessage("New password must be different from current password")
            .WithName("NewPassword");
    }
}
