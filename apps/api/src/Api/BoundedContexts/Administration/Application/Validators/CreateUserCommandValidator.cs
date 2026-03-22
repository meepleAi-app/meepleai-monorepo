using Api.BoundedContexts.Administration.Application.Commands;
using FluentValidation;

namespace Api.BoundedContexts.Administration.Application.Validators;

/// <summary>
/// Validator for CreateUserCommand.
/// Ensures email, password, display name, and role are valid.
/// </summary>
internal sealed class CreateUserCommandValidator : AbstractValidator<CreateUserCommand>
{
    private static readonly string[] AllowedRoles = { "admin", "editor", "user" };

    public CreateUserCommandValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty()
            .WithMessage("Email is required")
            .EmailAddress()
            .WithMessage("Email must be a valid email address")
            .MaximumLength(256)
            .WithMessage("Email must not exceed 256 characters");

        RuleFor(x => x.Password)
            .NotEmpty()
            .WithMessage("Password is required")
            .MinimumLength(8)
            .WithMessage("Password must be at least 8 characters");

        RuleFor(x => x.DisplayName)
            .NotEmpty()
            .WithMessage("DisplayName is required")
            .MaximumLength(100)
            .WithMessage("DisplayName must not exceed 100 characters");

        RuleFor(x => x.Role)
            .NotEmpty()
            .WithMessage("Role is required")
            .Must(r => AllowedRoles.Contains(r, StringComparer.OrdinalIgnoreCase))
            .WithMessage("Role must be one of: admin, editor, user");
    }
}
