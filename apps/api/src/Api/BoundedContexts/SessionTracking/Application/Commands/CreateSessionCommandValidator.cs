using MediatR;
using FluentValidation;

namespace Api.BoundedContexts.SessionTracking.Application.Commands;

public class CreateSessionCommandValidator : AbstractValidator<CreateSessionCommand>
{
    public CreateSessionCommandValidator()
    {
        RuleFor(x => x.SessionType)
            .NotEmpty()
            .Must(t => string.Equals(t, "Generic", StringComparison.Ordinal) || 
                      string.Equals(t, "GameSpecific", StringComparison.Ordinal))
            .WithMessage("SessionType must be 'Generic' or 'GameSpecific'");

        RuleFor(x => x.GameId)
            .NotEmpty()
            .WithMessage("GameId is required for all sessions");

        RuleFor(x => x.Participants)
            .NotEmpty()
            .WithMessage("At least one participant required")
            .Must(p => p.Count <= 20)
            .WithMessage("Maximum 20 participants allowed")
            .Must(p => p.Any(participant => participant.IsOwner))
            .WithMessage("At least one participant must be owner");

        RuleForEach(x => x.Participants).ChildRules(participant =>
        {
            participant.RuleFor(p => p.DisplayName)
                .NotEmpty()
                .MaximumLength(50);
        });
    }
}