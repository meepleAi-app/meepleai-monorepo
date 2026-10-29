using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.GameManagement.Application.Commands.GameNights;

/// <summary>
/// Organizer withdraws an invitation sent by email (#4721). A pending or accepted one becomes
/// <c>Cancelled</c>: its link answers 410 from then on, an accepted guest loses the seat, and nobody is
/// emailed. Declined and expired invitations stay as history and are refused with a conflict.
/// </summary>
/// <param name="GameNightId">The night the invitation belongs to (must be owned by <paramref name="OrganizerUserId"/>).</param>
/// <param name="InvitationId">The invitation to withdraw; it must belong to <paramref name="GameNightId"/>.</param>
/// <param name="OrganizerUserId">Authenticated caller, validated against the night's organizer.</param>
internal sealed record CancelGameNightInvitationCommand(
    Guid GameNightId,
    Guid InvitationId,
    Guid OrganizerUserId) : ICommand;
