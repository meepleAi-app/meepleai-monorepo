using Api.BoundedContexts.GameManagement.Domain.Enums;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.GameManagement.Application.Commands.GameNights;

/// <summary>
/// Command to respond (RSVP) to a game night invitation.
/// Issue #46: GameNight API endpoints.
/// </summary>
internal record RespondToGameNightCommand(
    Guid GameNightId,
    Guid UserId,
    RsvpStatus Response
) : ICommand;
