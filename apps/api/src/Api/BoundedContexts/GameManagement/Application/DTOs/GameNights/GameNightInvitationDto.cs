namespace Api.BoundedContexts.GameManagement.Application.DTOs.GameNights;

/// <summary>
/// Admin/organizer view of a game-night invitation, surfaces persistence-level
/// fields (Token, ExpiresAt, RespondedAt, audit) needed to manage outstanding
/// invitations.
/// Issue #607 (Wave A.5a): GameNight token-based RSVP backend extension.
/// </summary>
/// <remarks>
/// Returned by <c>POST /api/v1/game-nights/{gameNightId}/invitations</c>
/// (status 201 Created) and by the organizer-facing list endpoint. Distinct from
/// <see cref="PublicGameNightInvitationDto"/> which is the unauthenticated,
/// token-addressable surface and intentionally hides organizer-only fields.
/// </remarks>
public sealed record GameNightInvitationDto(
    Guid Id,
    string Token,
    Guid GameNightId,
    string Email,
    string Status,
    DateTimeOffset ExpiresAt,
    DateTimeOffset? RespondedAt,
    Guid? RespondedByUserId,
    DateTimeOffset CreatedAt,
    Guid CreatedBy);
