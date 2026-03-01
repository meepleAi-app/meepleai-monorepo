using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.GameManagement.Application.Commands.LiveSessions;

/// <summary>
/// Command to update the turn order of players in a live game session.
/// Issue #4749: CQRS commands for live sessions.
/// </summary>
internal record UpdatePlayerOrderCommand(
    Guid SessionId,
    List<Guid> PlayerIds
) : ICommand;
