using Api.SharedKernel.Application.Interfaces;
using FluentValidation;

namespace Api.BoundedContexts.SessionTracking.Application.Commands;

/// <summary>
/// Session Flow v2.1 — Plan 1bis T1.
/// Advances the turn index of a session to the next participant in the
/// stored turn order (cyclic). Emits a <c>turn_advanced</c> diary event.
/// The session owner is the only caller allowed to mutate turn state.
/// </summary>
public sealed record AdvanceTurnCommand(
    Guid SessionId,
    Guid UserId
) : ICommand<AdvanceTurnCommandResult>;

/// <summary>
/// Result of an <see cref="AdvanceTurnCommand"/> execution: the prior and new
/// turn indices plus the resolved participant IDs. Exposed via the HTTP
/// endpoint so clients can refresh their "current player" chip without a
/// round-trip to the session read model.
/// </summary>
public sealed record AdvanceTurnCommandResult(
    int FromIndex,
    int ToIndex,
    Guid FromParticipantId,
    Guid ToParticipantId
);

public sealed class AdvanceTurnCommandValidator : AbstractValidator<AdvanceTurnCommand>
{
    public AdvanceTurnCommandValidator()
    {
        RuleFor(x => x.SessionId).NotEmpty();
        RuleFor(x => x.UserId).NotEmpty();
    }
}
