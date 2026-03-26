using Api.BoundedContexts.Authentication.Application.Commands;
using FluentValidation;

namespace Api.BoundedContexts.Authentication.Application.Commands.Sessions;

/// <summary>
/// Validates LogoutAllDevicesCommand.
/// Ensures user ID is provided and optional fields have length limits.
/// </summary>
internal sealed class LogoutAllDevicesCommandValidator : AbstractValidator<LogoutAllDevicesCommand>
{
    public LogoutAllDevicesCommandValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty()
            .WithMessage("User ID is required");

        RuleFor(x => x.CurrentSessionTokenHash)
            .MaximumLength(500)
            .WithMessage("Session token hash must not exceed 500 characters")
            .When(x => x.CurrentSessionTokenHash != null);

        RuleFor(x => x.Password)
            .MinimumLength(8)
            .WithMessage("Password must be at least 8 characters")
            .When(x => x.Password != null);
    }
}
