namespace Api.BoundedContexts.GameManagement.Application.DTOs.GameNights;

/// <summary>
/// Public, token-addressable view of a game-night invitation.
/// Issue #607 (Wave A.5a): GameNight token-based RSVP backend extension.
/// </summary>
/// <remarks>
/// <para>
/// Surface area aligned 1:1 with the mockup <c>InviteBody</c> component
/// (`admin-mockups/design_files/sp3-accept-invite.jsx`). The endpoint
/// <c>GET /api/v1/game-nights/invitations/{token}</c> returns this shape
/// with no authentication required.
/// </para>
/// <para>
/// <see cref="AlreadyRespondedAs"/> is populated to <c>"Accepted"</c> or
/// <c>"Declined"</c> when the invitation has already been responded to,
/// allowing the frontend to skip the action affordance and show a
/// confirmation surface instead. <c>null</c> for pending invitations.
/// </para>
/// </remarks>
public sealed record PublicGameNightInvitationDto(
    string Token,
    string Status,
    DateTimeOffset ExpiresAt,
    DateTimeOffset? RespondedAt,
    Guid HostUserId,
    string HostDisplayName,
    string? HostAvatarUrl,
    string? HostWelcomeMessage,
    Guid GameNightId,
    string Title,
    DateTimeOffset ScheduledAt,
    string? Location,
    int? DurationMinutes,
    int ExpectedPlayers,
    int AcceptedSoFar,
    Guid? PrimaryGameId,
    string? PrimaryGameName,
    string? PrimaryGameImageUrl,
    string? AlreadyRespondedAs);
