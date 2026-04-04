using FluentValidation;

namespace Api.BoundedContexts.Authentication.Application.Commands.Invitation;

/// <summary>
/// Validator for ResendInvitationCommand.
/// Issue #124: User invitation system.
/// </summary>
internal sealed class ResendInvitationCommandValidator : AbstractValidator<ResendInvitationCommand>
{
    public ResendInvitationCommandValidator()
    {
        RuleFor(x => x.InvitationId)
            .NotEqual(Guid.Empty)
            .WithMessage("InvitationId is required");

        RuleFor(x => x.ResendByUserId)
            .NotEqual(Guid.Empty)
            .WithMessage("ResendByUserId is required");
    }
}
