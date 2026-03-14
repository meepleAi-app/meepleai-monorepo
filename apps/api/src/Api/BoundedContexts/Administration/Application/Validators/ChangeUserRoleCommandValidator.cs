using Api.BoundedContexts.Administration.Application.Commands;
using FluentValidation;

namespace Api.BoundedContexts.Administration.Application.Validators;

/// <summary>
/// Validator for ChangeUserRoleCommand.
/// Ensures UserId and NewRole are valid, and Reason does not exceed 500 characters.
/// </summary>
internal sealed class ChangeUserRoleCommandValidator : AbstractValidator<ChangeUserRoleCommand>
{
    private static readonly string[] AllowedRoles = { "Admin", "Editor", "User" };

    public ChangeUserRoleCommandValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty()
            .WithMessage("UserId is required")
            .Must(BeValidGuid)
            .WithMessage("UserId must be a valid GUID format");

        RuleFor(x => x.NewRole)
            .NotEmpty()
            .WithMessage("NewRole is required")
            .Must(role => AllowedRoles.Contains(role, StringComparer.Ordinal))
            .WithMessage($"NewRole must be one of: {string.Join(", ", AllowedRoles)}");

        RuleFor(x => x.Reason)
            .MaximumLength(500)
            .WithMessage("Role change reason cannot exceed 500 characters");
    }

    private static bool BeValidGuid(string? userId)
    {
        return !string.IsNullOrWhiteSpace(userId) && Guid.TryParse(userId, out _);
    }
}
