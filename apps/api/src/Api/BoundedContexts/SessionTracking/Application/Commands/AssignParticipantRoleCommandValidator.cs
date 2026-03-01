using Api.BoundedContexts.SessionTracking.Domain.Enums;
using FluentValidation;

namespace Api.BoundedContexts.SessionTracking.Application.Commands;

public class AssignParticipantRoleCommandValidator : AbstractValidator<AssignParticipantRoleCommand>
{
    public AssignParticipantRoleCommandValidator()
    {
        RuleFor(x => x.SessionId)
            .NotEmpty()
            .WithMessage("SessionId is required");

        RuleFor(x => x.ParticipantId)
            .NotEmpty()
            .WithMessage("ParticipantId is required");

        RuleFor(x => x.NewRole)
            .IsInEnum()
            .WithMessage("NewRole must be a valid ParticipantRole");

        RuleFor(x => x.RequesterId)
            .NotEmpty()
            .WithMessage("RequesterId is required");
    }
}
