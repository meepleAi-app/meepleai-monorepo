using Api.BoundedContexts.SessionTracking.Domain.Enums;
using Api.BoundedContexts.SessionTracking.Domain.Services;
using MediatR;

namespace Api.BoundedContexts.SessionTracking.Application.Commands;

/// <summary>
/// Kicks a participant from a session. Requires Host role.
/// Issue #4765 - Player Action Endpoints + Host Validation
/// </summary>
public record KickParticipantCommand(
    Guid SessionId,
    Guid ParticipantId,
    Guid RequesterId
) : IRequest<KickParticipantResult>, IRequireSessionRole
{
    public ParticipantRole MinimumRole => ParticipantRole.Host;
}

public record KickParticipantResult(
    Guid ParticipantId,
    string DisplayName
);
