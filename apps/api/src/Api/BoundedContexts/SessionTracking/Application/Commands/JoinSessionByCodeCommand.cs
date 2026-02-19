using MediatR;

namespace Api.BoundedContexts.SessionTracking.Application.Commands;

/// <summary>
/// Joins a session using the 6-character session code.
/// Issue #4766 - Session Join via Code + Active Player Roles
/// </summary>
public record JoinSessionByCodeCommand(
    string SessionCode,
    Guid UserId,
    string DisplayName
) : IRequest<JoinSessionByCodeResult>;

public record JoinSessionByCodeResult(
    Guid SessionId,
    string SessionCode,
    Guid ParticipantId,
    string DisplayName,
    string Role,
    int JoinOrder
);
