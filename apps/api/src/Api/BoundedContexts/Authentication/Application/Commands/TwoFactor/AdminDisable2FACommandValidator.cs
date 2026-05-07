using FluentValidation;

namespace Api.BoundedContexts.Authentication.Application.Commands.TwoFactor;

/// <summary>
/// Validates AdminDisable2FACommand.
/// Ensures both admin and target user IDs are provided.
/// </summary>
internal sealed class AdminDisable2FACommandValidator : AbstractValidator<AdminDisable2FACommand>
{
    public AdminDisable2FACommandValidator()
    {
        RuleFor(x => x.AdminUserId)
            .NotEmpty()
            .WithMessage("Admin user ID is required");

        RuleFor(x => x.TargetUserId)
            .NotEmpty()
            .WithMessage("Target user ID is required");

        // I4: admin re-auth password required (verified server-side).
        RuleFor(x => x.AdminPassword)
            .NotEmpty()
            .WithMessage("Admin password is required");
    }
}
