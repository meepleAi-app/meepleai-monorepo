namespace Api.BoundedContexts.GameManagement.Application.DTOs.GameNights;

/// <summary>
/// Organizer view of a game-night invitation just created (or sent again): its id, the address, the state and the
/// dates needed to manage it.
/// Issue #607 (Wave A.5a): GameNight token-based RSVP backend extension.
/// </summary>
/// <remarks>
/// <para>
/// Returned by <c>POST /api/v1/game-nights/{gameNightId}/invitations</c> (status 201 Created) and only there: the
/// organizer-facing list is <see cref="GameNightGuestInvitationDto"/>. Distinct from
/// <see cref="PublicGameNightInvitationDto"/>, the unauthenticated, token-addressable surface.
/// </para>
/// <para>
/// <b>It carries no RSVP token (#4874).</b> The token is the credential of the anonymous
/// <c>POST /api/v1/game-nights/invitations/{token}/respond</c>; whoever holds it answers in the guest's place. It is
/// written in the guest's email and nowhere else, like the unsubscribe key. The organizer correlates and withdraws
/// by <see cref="Id"/>.
/// </para>
/// </remarks>
public sealed record GameNightInvitationDto(
    Guid Id,
    Guid GameNightId,
    string Email,
    string Status,
    DateTimeOffset ExpiresAt,
    DateTimeOffset? RespondedAt,
    Guid? RespondedByUserId,
    DateTimeOffset CreatedAt,
    Guid CreatedBy);
