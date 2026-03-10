using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.GameManagement.Application.Commands.GameNights;

/// <summary>
/// Command to create a new game night event in Draft status.
/// Issue #46: GameNight API endpoints.
/// </summary>
internal record CreateGameNightCommand(
    Guid UserId,
    string Title,
    DateTimeOffset ScheduledAt,
    string? Description = null,
    string? Location = null,
    int? MaxPlayers = null,
    List<Guid>? GameIds = null,
    List<Guid>? InvitedUserIds = null
) : ICommand<Guid>;
