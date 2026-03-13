using MediatR;

namespace Api.BoundedContexts.GameManagement.Application.Commands.Session;

/// <summary>
/// Joins a session via invite PIN or link token.
/// E3-1: Session Invite Flow — supports both registered users and guests.
/// </summary>
public sealed record JoinSessionCommand(
    string Token,       // Either a PIN or a link token
    string? GuestName,  // Required for guests, null for registered users
    Guid? UserId        // Set if authenticated user
) : IRequest<JoinSessionResultDto>;

public sealed record JoinSessionResultDto(
    Guid ParticipantId,
    Guid SessionId,
    string ConnectionToken,
    string DisplayName,
    string Role);
