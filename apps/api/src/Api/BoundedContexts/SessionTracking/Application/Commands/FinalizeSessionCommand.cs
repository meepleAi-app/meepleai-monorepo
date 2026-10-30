using MediatR;

namespace Api.BoundedContexts.SessionTracking.Application.Commands;

/// <param name="SessionId">The session to finalize.</param>
/// <param name="FinalRanks">participantId → rank. The validator requires exactly 1..N with no gaps.</param>
/// <param name="RequestedBy">IDOR guard: authenticated caller — must be the session owner or a participant.</param>
/// <param name="NoWinner">
/// #4752: the session ended with no winner decided (a tie, or nobody scored). <see cref="FinalRanks"/> then
/// only orders the participants — 1..N is the one shape the validator accepts, so it cannot say "nobody
/// won" by itself — and NO participant is recorded as the winner in the finalized event, the diary entry
/// or the result. Without this flag the rank-1 participant would be a winner nobody picked.
/// </param>
public record FinalizeSessionCommand(
    Guid SessionId,
    Dictionary<Guid, int> FinalRanks,
    Guid RequestedBy,
    bool NoWinner = false
) : IRequest<FinalizeSessionResult>;

public record FinalizeSessionResult(
    Guid? WinnerId,
    Dictionary<Guid, decimal> FinalScores,
    bool LiveActiveWarning = false
);
