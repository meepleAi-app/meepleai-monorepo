using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.GameManagement.Application.Commands.LiveSessions;

/// <summary>
/// Command to record a new score entry for a player in a live session.
/// Issue #4749: CQRS commands for live sessions.
/// </summary>
internal record RecordLiveSessionScoreCommand(
    Guid SessionId,
    Guid PlayerId,
    int Round,
    string Dimension,
    int Value,
    string? Unit = null
) : ICommand;
