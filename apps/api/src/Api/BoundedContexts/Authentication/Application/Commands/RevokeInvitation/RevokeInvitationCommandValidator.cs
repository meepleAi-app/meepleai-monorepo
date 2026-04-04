using FluentValidation;

namespace Api.BoundedContexts.Authentication.Application.Commands.RevokeInvitation;

/// <summary>
/// Validates RevokeInvitationCommand.
/// Ensures invitation ID and admin user ID are provided.
/// </summary>
internal sealed class RevokeInvitationCommandValidator : AbstractValidator<RevokeInvitationCommand>
{
    public RevokeInvitationCommandValidator()
    {
        RuleFor(x => x.InvitationId)
            .NotEmpty()
            .WithMessage("Invitation ID is required");

        RuleFor(x => x.AdminUserId)
            .NotEmpty()
            .WithMessage("Admin user ID is required");
    }
}
