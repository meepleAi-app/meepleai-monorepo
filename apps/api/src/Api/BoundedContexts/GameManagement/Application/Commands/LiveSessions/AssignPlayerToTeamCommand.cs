using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.GameManagement.Application.Commands.LiveSessions;

/// <summary>
/// Command to assign a player to a team in a live game session.
/// Issue #4749: CQRS commands for live sessions.
/// </summary>
internal record AssignPlayerToTeamCommand(
    Guid SessionId,
    Guid PlayerId,
    Guid TeamId
) : ICommand;
