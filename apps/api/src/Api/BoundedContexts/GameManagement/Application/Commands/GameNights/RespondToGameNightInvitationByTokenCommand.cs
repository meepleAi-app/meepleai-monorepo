using Api.BoundedContexts.GameManagement.Domain.Enums;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.GameManagement.Application.Commands.GameNights;

/// <summary>
/// Public command to record a guest RSVP against a token-addressable
/// invitation. Anonymous-friendly: <paramref name="ResponderUserId"/> is
/// optional and only populated when the responding HTTP request happened to
/// carry a valid auth cookie.
/// Issue #607 (Wave A.5a): GameNight token-based RSVP backend extension.
/// </summary>
/// <param name="Token">The 22-character invitation token.</param>
/// <param name="Response">Either <see cref="GameNightInvitationStatus.Accepted"/> or <see cref="GameNightInvitationStatus.Declined"/>.</param>
/// <param name="ResponderUserId">Optional responder identity (cookie-authenticated guests only).</param>
/// <param name="ResponderDisplayName">
/// Optional guest-supplied display name (issue #1169). The invitation email is
/// already bound by the token, so the guest only types a name. Trimmed and
/// capped at 120 chars by the validator; null when omitted by the caller or
/// when the response is being made through a flow that did not collect a name.
/// </param>
internal sealed record RespondToGameNightInvitationByTokenCommand(
    string Token,
    GameNightInvitationStatus Response,
    Guid? ResponderUserId,
    string? ResponderDisplayName = null) : ICommand;
