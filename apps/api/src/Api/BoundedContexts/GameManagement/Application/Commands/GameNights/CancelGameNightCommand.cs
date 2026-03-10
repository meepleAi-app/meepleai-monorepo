using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.GameManagement.Application.Commands.GameNights;

/// <summary>
/// Command to cancel a game night event.
/// Issue #46: GameNight API endpoints.
/// </summary>
internal record CancelGameNightCommand(
    Guid GameNightId,
    Guid UserId
) : ICommand;
