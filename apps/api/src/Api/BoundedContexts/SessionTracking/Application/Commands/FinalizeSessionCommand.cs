using MediatR;

namespace Api.BoundedContexts.SessionTracking.Application.Commands;

public record FinalizeSessionCommand(
    Guid SessionId,
    Dictionary<Guid, int> FinalRanks, // participantId → rank
    Guid RequestedBy // IDOR guard: authenticated caller — must be the session owner or a participant.
) : IRequest<FinalizeSessionResult>;

public record FinalizeSessionResult(
    Guid? WinnerId,
    Dictionary<Guid, decimal> FinalScores,
    bool LiveActiveWarning = false
);
