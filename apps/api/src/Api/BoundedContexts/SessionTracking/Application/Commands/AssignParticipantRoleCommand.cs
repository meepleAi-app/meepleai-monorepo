using Api.BoundedContexts.SessionTracking.Domain.Enums;
using Api.BoundedContexts.SessionTracking.Domain.Services;
using MediatR;

namespace Api.BoundedContexts.SessionTracking.Application.Commands;

/// <summary>
/// Assigns a new role to a participant. Host-only action.
/// Issue #4766 - Session Join via Code + Active Player Roles
/// </summary>
public record AssignParticipantRoleCommand(
    Guid SessionId,
    Guid ParticipantId,
    ParticipantRole NewRole,
    Guid RequesterId
) : IRequest<AssignParticipantRoleResult>, IRequireSessionRole
{
    public ParticipantRole MinimumRole => ParticipantRole.Host;
}

public record AssignParticipantRoleResult(
    Guid ParticipantId,
    string DisplayName,
    string PreviousRole,
    string NewRole
);
