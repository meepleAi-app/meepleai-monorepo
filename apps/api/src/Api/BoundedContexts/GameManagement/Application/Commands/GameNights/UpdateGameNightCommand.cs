using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.GameManagement.Application.Commands.GameNights;

/// <summary>
/// Command to update an existing game night event.
/// Issue #46: GameNight API endpoints.
/// </summary>
internal record UpdateGameNightCommand(
    Guid GameNightId,
    Guid UserId,
    string Title,
    DateTimeOffset ScheduledAt,
    string? Description = null,
    string? Location = null,
    int? MaxPlayers = null,
    List<Guid>? GameIds = null
) : ICommand;
