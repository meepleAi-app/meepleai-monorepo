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
/// <para>
/// <see cref="RespondedByName"/> (issue #1169) carries the guest-supplied
/// display name captured at RSVP time. Lets the UI render confirmation
/// surfaces like "Already responded as 'Marco'." Null for pending
/// invitations and for historical responses that pre-date the column.
/// </para>
/// <para>
/// <b>A link that is no longer alive shows nothing of the night</b> (#4745, #4856). When the invitation is
/// withdrawn or expired (also a pending one past its cutoff, which the respond endpoint already refuses), or when
/// the night was cancelled, the answer carries only what the page needs to say so: <see cref="Token"/>,
/// <see cref="Status"/>, <see cref="ExpiresAt"/> and the host's name («ask them to send it again»). The rest is
/// empty (<c>Title</c> blank, <c>ScheduledAt</c> the default value, ids the empty guid, counts zero, the optional
/// fields null) so the shape does not change for the clients that read it: whoever was taken off a night, or
/// kept the link of one that is off, cannot read where and when it was.
/// </para>
/// <para>
/// <see cref="NightCancelled"/> is true when the NIGHT was cancelled, whatever the state of the invitation (which
/// keeps its own: the invitation aggregate is not touched by the cancellation). The page tells it apart from a
/// withdrawn or expired invitation.
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
    string? AlreadyRespondedAs,
    string? RespondedByName = null,
    bool NightCancelled = false);
