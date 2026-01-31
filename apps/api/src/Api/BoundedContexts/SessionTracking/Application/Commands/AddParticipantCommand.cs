using MediatR;

namespace Api.BoundedContexts.SessionTracking.Application.Commands;

public record AddParticipantCommand(
    Guid SessionId,
    string DisplayName,
    Guid? UserId
) : IRequest<AddParticipantResult>;

public record AddParticipantResult(
    Guid ParticipantId,
    int JoinOrder
);
