using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.GameManagement.Application.Commands.LiveSessions;

/// <summary>
/// Command to remove a player from a live game session.
/// Issue #4749: CQRS commands for live sessions.
/// </summary>
internal record RemovePlayerFromLiveSessionCommand(
    Guid SessionId,
    Guid PlayerId
) : ICommand;
