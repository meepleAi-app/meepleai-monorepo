// Queries for session invite functionality (Issue #3354).

using MediatR;

namespace Api.BoundedContexts.SessionTracking.Application.Queries;

// ============================================================================
// Get Session By Invite Token Query
// ============================================================================

/// <summary>
/// Query to get session information by invite token.
/// </summary>
public sealed record GetSessionByInviteQuery(
    string InviteToken
) : IRequest<SessionInviteResponse>;

/// <summary>
/// Response containing session invite information.
/// </summary>
public sealed record SessionInviteResponse(
    Guid SessionId,
    string SessionCode,
    string? GameName,
    string? GameImageUrl,
    DateTime SessionDate,
    string? Location,
    string Status,
    int ParticipantCount,
    string OwnerDisplayName,
    bool CanJoin,
    string? ReasonCannotJoin
);

// ============================================================================
// Generate Invite Token Command
// ============================================================================

/// <summary>
/// Command to generate a new invite token for a session.
/// </summary>
public sealed record GenerateInviteTokenCommand(
    Guid SessionId,
    Guid RequestedBy,
    int? ExpiresInHours = null
) : IRequest<InviteTokenResponse>;

/// <summary>
/// Response containing the generated invite URL and token.
/// </summary>
public sealed record InviteTokenResponse(
    string InviteToken,
    string InviteUrl,
    DateTime? ExpiresAt,
    string SessionCode,
    string QrCodeDataUrl
);

// ============================================================================
// Join Session By Invite Command
// ============================================================================

/// <summary>
/// Command to join a session using an invite token.
/// </summary>
public sealed record JoinSessionByInviteCommand(
    string InviteToken,
    Guid UserId,
    string DisplayName
) : IRequest<JoinSessionResponse>;

/// <summary>
/// Response after joining a session.
/// </summary>
public sealed record JoinSessionResponse(
    Guid SessionId,
    string SessionCode,
    Guid ParticipantId,
    string DisplayName,
    int JoinOrder
);
