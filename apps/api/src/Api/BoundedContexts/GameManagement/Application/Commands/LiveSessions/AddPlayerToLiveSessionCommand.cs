using Api.BoundedContexts.GameManagement.Domain.Enums;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.GameManagement.Application.Commands.LiveSessions;

/// <summary>
/// Command to add a player to a live game session.
/// Returns the new player's ID.
/// Issue #4749: CQRS commands for live sessions.
/// </summary>
internal record AddPlayerToLiveSessionCommand(
    Guid SessionId,
    string DisplayName,
    PlayerColor Color,
    Guid? UserId = null,
    PlayerRole? Role = null,
    string? AvatarUrl = null
) : ICommand<Guid>;
