using MediatR;

namespace Api.BoundedContexts.SessionTracking.Application.Commands;

public record AddParticipantCommand(
    Guid SessionId,
    string DisplayName,
    Guid? UserId,
    Guid RequestedBy // IDOR guard: authenticated caller — must be the session owner or a participant.
) : IRequest<AddParticipantResult>;

public record AddParticipantResult(
    Guid ParticipantId,
    int JoinOrder
);
