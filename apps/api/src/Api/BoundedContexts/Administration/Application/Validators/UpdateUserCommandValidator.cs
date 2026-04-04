using Api.BoundedContexts.Administration.Application.Commands;
using FluentValidation;

namespace Api.BoundedContexts.Administration.Application.Validators;

/// <summary>
/// Validator for UpdateUserCommand.
/// Ensures user ID is valid and optional fields have proper format.
/// </summary>
internal sealed class UpdateUserCommandValidator : AbstractValidator<UpdateUserCommand>
{
    private static readonly string[] AllowedRoles = { "admin", "editor", "user" };

    public UpdateUserCommandValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty()
            .WithMessage("UserId is required")
            .Must(BeValidGuid)
            .WithMessage("UserId must be a valid GUID format");

        RuleFor(x => x.Email)
            .EmailAddress()
            .WithMessage("Email must be a valid email address")
            .MaximumLength(256)
            .WithMessage("Email must not exceed 256 characters")
            .When(x => !string.IsNullOrEmpty(x.Email));

        RuleFor(x => x.DisplayName)
            .MaximumLength(100)
            .WithMessage("DisplayName must not exceed 100 characters")
            .When(x => !string.IsNullOrEmpty(x.DisplayName));

        RuleFor(x => x.Role)
            .Must(r => AllowedRoles.Contains(r!, StringComparer.OrdinalIgnoreCase))
            .WithMessage("Role must be one of: admin, editor, user")
            .When(x => !string.IsNullOrEmpty(x.Role));
    }

    private static bool BeValidGuid(string? value)
    {
        return !string.IsNullOrWhiteSpace(value) && Guid.TryParse(value, out _);
    }
}
