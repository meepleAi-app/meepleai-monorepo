using Api.BoundedContexts.GameManagement.Application.DTOs.GameNights;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.GameManagement.Application.Commands.GameNights;

/// <summary>
/// Organizer-issued command to create a token-based invitation for a single
/// email address. Sends the invitation email synchronously after persistence
/// (D1 a — see executive spec §5).
/// Issue #607 (Wave A.5a): GameNight token-based RSVP backend extension.
/// </summary>
/// <param name="GameNightId">Target game night (must be owned by <paramref name="OrganizerUserId"/>).</param>
/// <param name="Email">Recipient email — case-insensitive, normalized in the aggregate factory.</param>
/// <param name="OrganizerUserId">Authenticated caller, validated against the parent event's organizer.</param>
/// <param name="ResendIfPending">
/// When a pending invitation for this email already exists: <c>false</c> (default) rejects with a conflict,
/// <c>true</c> sends the email of that same invitation again and returns it. The creation of a game night
/// asks for <c>true</c>, because it is repeated on purpose and the earlier attempt may have saved the
/// invitation and then failed to deliver it (#4692).
/// </param>
internal sealed record CreateGameNightInvitationByEmailCommand(
    Guid GameNightId,
    string Email,
    Guid OrganizerUserId,
    bool ResendIfPending = false) : ICommand<GameNightInvitationDto>;
