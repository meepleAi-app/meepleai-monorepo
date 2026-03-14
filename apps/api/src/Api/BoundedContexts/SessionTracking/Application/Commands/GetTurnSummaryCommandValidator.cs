using FluentValidation;

namespace Api.BoundedContexts.SessionTracking.Application.Commands;

/// <summary>
/// Validates GetTurnSummaryCommand.
/// Issue #277 - Turn Summary AI Feature
/// </summary>
public class GetTurnSummaryCommandValidator : AbstractValidator<GetTurnSummaryCommand>
{
    public GetTurnSummaryCommandValidator()
    {
        RuleFor(x => x.SessionId)
            .NotEmpty();

        RuleFor(x => x.RequesterId)
            .NotEmpty();

        RuleFor(x => x.LastNEvents)
            .InclusiveBetween(1, 100)
            .When(x => x.LastNEvents.HasValue);

        RuleFor(x => x.FromPhase)
            .GreaterThanOrEqualTo(0)
            .When(x => x.FromPhase.HasValue);

        RuleFor(x => x.ToPhase)
            .GreaterThanOrEqualTo(0)
            .When(x => x.ToPhase.HasValue);

        RuleFor(x => x)
            .Must(x => x.LastNEvents.HasValue || x.FromPhase.HasValue || x.ToPhase.HasValue)
            .WithMessage("At least one of LastNEvents, FromPhase, or ToPhase must be provided.");

        RuleFor(x => x)
            .Must(x => !x.FromPhase.HasValue || !x.ToPhase.HasValue || x.ToPhase.Value >= x.FromPhase.Value)
            .When(x => x.FromPhase.HasValue && x.ToPhase.HasValue)
            .WithMessage("ToPhase must be greater than or equal to FromPhase.");
    }
}
