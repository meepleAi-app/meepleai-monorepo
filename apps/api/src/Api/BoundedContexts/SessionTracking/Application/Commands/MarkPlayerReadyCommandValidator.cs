using FluentValidation;

namespace Api.BoundedContexts.SessionTracking.Application.Commands;

public class MarkPlayerReadyCommandValidator : AbstractValidator<MarkPlayerReadyCommand>
{
    public MarkPlayerReadyCommandValidator()
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
    }
}
