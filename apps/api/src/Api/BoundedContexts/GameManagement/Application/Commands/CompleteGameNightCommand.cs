using FluentValidation;
using MediatR;

namespace Api.BoundedContexts.GameManagement.Application.Commands;

/// <summary>
/// Session Flow v2.1 — Plan 1bis T3.
/// Completes an in-progress ad-hoc game night: cascade-finalizes all
/// non-finalized sessions, marks GameNightSession links as Completed,
/// transitions the GameNightEvent to Completed, and emits diary events.
/// </summary>
public sealed record CompleteGameNightCommand(
    Guid GameNightEventId,
    Guid UserId
) : IRequest<CompleteGameNightResult>;

/// <summary>
/// Result of a <see cref="CompleteGameNightCommand"/> execution.
/// </summary>
public sealed record CompleteGameNightResult(
    Guid GameNightEventId,
    int SessionCount,
    int FinalizedSessionCount,
    int DurationSeconds);

public sealed class CompleteGameNightCommandValidator : AbstractValidator<CompleteGameNightCommand>
{
    public CompleteGameNightCommandValidator()
    {
        RuleFor(x => x.GameNightEventId).NotEmpty();
        RuleFor(x => x.UserId).NotEmpty();
    }
}
