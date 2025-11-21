using Api.BoundedContexts.Authentication.Application.Commands;
using FluentValidation;

namespace Api.BoundedContexts.Authentication.Application.Validators;

/// <summary>
/// Validator for RegisterCommand.
/// Enforces email format, password complexity, display name requirements, and optional role validation.
/// Issue #1449: FluentValidation for Authentication CQRS pipeline
/// </summary>
public sealed class RegisterCommandValidator : AbstractValidator<RegisterCommand>
{
    private static readonly string[] ValidRoles = { "user", "editor", "admin" };

    public RegisterCommandValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty()
            .WithMessage("Email is required")
            .EmailAddress()
            .WithMessage("Email must be a valid email address")
            .MaximumLength(255)
            .WithMessage("Email must not exceed 255 characters");

        RuleFor(x => x.Password)
            .NotEmpty()
            .WithMessage("Password is required")
            .MinimumLength(8)
            .WithMessage("Password must be at least 8 characters")
            .MaximumLength(128)
            .WithMessage("Password must not exceed 128 characters")
            .Matches(@"[A-Z]")
            .WithMessage("Password must contain at least one uppercase letter")
            .Matches(@"[a-z]")
            .WithMessage("Password must contain at least one lowercase letter")
            .Matches(@"[0-9]")
            .WithMessage("Password must contain at least one digit")
            .Matches(@"[^a-zA-Z0-9]")
            .WithMessage("Password must contain at least one special character");

        RuleFor(x => x.DisplayName)
            .NotEmpty()
            .WithMessage("Display name is required")
            .MinimumLength(2)
            .WithMessage("Display name must be at least 2 characters")
            .MaximumLength(100)
            .WithMessage("Display name must not exceed 100 characters")
            .Matches(@"^[a-zA-Z0-9\s\-_\.]+$")
            .WithMessage("Display name can only contain letters, numbers, spaces, hyphens, underscores, and periods");

        RuleFor(x => x.Role)
            .Must(role => string.IsNullOrWhiteSpace(role) || ValidRoles.Contains(role.ToLowerInvariant()))
            .WithMessage($"Role must be one of: {string.Join(", ", ValidRoles)}");
    }
}
