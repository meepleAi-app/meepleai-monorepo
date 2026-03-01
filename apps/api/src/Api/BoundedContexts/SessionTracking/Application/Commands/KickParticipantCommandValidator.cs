using FluentValidation;

namespace Api.BoundedContexts.SessionTracking.Application.Commands;

public class KickParticipantCommandValidator : AbstractValidator<KickParticipantCommand>
{
    public KickParticipantCommandValidator()
    {
        RuleFor(x => x.SessionId)
            .NotEmpty()
            .WithMessage("SessionId is required");

        RuleFor(x => x.ParticipantId)
            .NotEmpty()
            .WithMessage("ParticipantId is required");

        RuleFor(x => x.RequesterId)
            .NotEmpty()
            .WithMessage("RequesterId is required");

        RuleFor(x => x)
            .Must(x => x.ParticipantId != x.RequesterId)
            .WithMessage("Cannot kick yourself");
    }
}
