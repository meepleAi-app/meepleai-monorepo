using MediatR;

namespace Api.BoundedContexts.GameManagement.Application.Queries.Session;

/// <summary>
/// Gets all participants (active and left) for a session.
/// E3-1: Session Invite Flow.
/// </summary>
public sealed record GetSessionParticipantsQuery(Guid SessionId) : IRequest<IReadOnlyList<SessionParticipantDto>>;

public sealed record SessionParticipantDto(
    Guid Id,
    Guid SessionId,
    Guid? UserId,
    string? GuestName,
    string DisplayName,
    string Role,
    bool AgentAccessEnabled,
    DateTime JoinedAt,
    DateTime? LeftAt);
