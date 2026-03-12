using FluentValidation;

namespace Api.BoundedContexts.Authentication.Application.Commands.Invitation;

/// <summary>
/// Validator for SendInvitationCommand.
/// Issue #124: User invitation system.
/// </summary>
internal sealed class SendInvitationCommandValidator : AbstractValidator<SendInvitationCommand>
{
    private static readonly string[] ValidRoles = { "User", "Editor", "Admin" };

    public SendInvitationCommandValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty()
            .WithMessage("Email is required")
            .EmailAddress()
            .WithMessage("Email must be a valid email address")
            .MaximumLength(256)
            .WithMessage("Email must not exceed 256 characters");

        RuleFor(x => x.Role)
            .NotEmpty()
            .WithMessage("Role is required")
            .Must(role => ValidRoles.Contains(role, StringComparer.OrdinalIgnoreCase))
            .WithMessage($"Role must be one of: {string.Join(", ", ValidRoles)}");

        RuleFor(x => x.InvitedByUserId)
            .NotEqual(Guid.Empty)
            .WithMessage("InvitedByUserId is required");
    }
}
