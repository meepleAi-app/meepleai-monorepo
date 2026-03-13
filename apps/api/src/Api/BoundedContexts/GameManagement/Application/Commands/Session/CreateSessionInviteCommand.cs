using MediatR;

namespace Api.BoundedContexts.GameManagement.Application.Commands.Session;

/// <summary>
/// Creates a session invite with PIN and link token.
/// E3-1: Session Invite Flow — host only.
/// </summary>
public sealed record CreateSessionInviteCommand(
    Guid SessionId,
    Guid UserId,
    int MaxUses = 10,
    int ExpiryMinutes = 30) : IRequest<SessionInviteResultDto>;

public sealed record SessionInviteResultDto(
    string Pin,
    string LinkToken,
    DateTime ExpiresAt,
    int MaxUses);
