using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.GameManagement.Application.Commands.LiveSessions;

/// <summary>
/// Command to create a team in a live game session.
/// Returns the new team's ID.
/// Issue #4749: CQRS commands for live sessions.
/// </summary>
internal record CreateLiveSessionTeamCommand(
    Guid SessionId,
    string Name,
    string Color
) : ICommand<Guid>;
