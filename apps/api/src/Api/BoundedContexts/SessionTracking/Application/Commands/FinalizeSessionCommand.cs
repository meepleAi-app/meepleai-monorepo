using MediatR;

namespace Api.BoundedContexts.SessionTracking.Application.Commands;

public record FinalizeSessionCommand(
    Guid SessionId,
    Dictionary<Guid, int> FinalRanks // participantId → rank
) : IRequest<FinalizeSessionResult>;

public record FinalizeSessionResult(
    Guid? WinnerId,
    Dictionary<Guid, decimal> FinalScores
);
