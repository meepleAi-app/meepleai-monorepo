using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.GameManagement.Application.Commands.GameNights;

/// <summary>
/// Command to update an existing game night event.
/// Issue #46: GameNight API endpoints.
/// Issue #4754: <c>TimeZoneId</c> null keeps the zone already stored (see <c>GameNightEvent.Update</c>).
/// </summary>
internal record UpdateGameNightCommand(
    Guid GameNightId,
    Guid UserId,
    string Title,
    DateTimeOffset ScheduledAt,
    string? Description = null,
    string? Location = null,
    int? MaxPlayers = null,
    List<Guid>? GameIds = null,
    string? TimeZoneId = null
) : ICommand;
