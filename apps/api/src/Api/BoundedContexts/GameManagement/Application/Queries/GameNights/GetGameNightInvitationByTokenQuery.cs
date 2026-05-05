using Api.BoundedContexts.GameManagement.Application.DTOs.GameNights;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.GameManagement.Application.Queries.GameNights;

/// <summary>
/// Public query to fetch the token-addressable view of a game-night
/// invitation. No authentication required — token possession is the
/// authorization mechanism.
/// Issue #607 (Wave A.5a): GameNight token-based RSVP backend extension.
/// </summary>
internal sealed record GetGameNightInvitationByTokenQuery(
    string Token) : IQuery<PublicGameNightInvitationDto?>;
