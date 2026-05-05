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
internal sealed record CreateGameNightInvitationByEmailCommand(
    Guid GameNightId,
    string Email,
    Guid OrganizerUserId) : ICommand<GameNightInvitationDto>;
