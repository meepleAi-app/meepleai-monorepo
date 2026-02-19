using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.GameManagement.Application.Commands.LiveSessions;

/// <summary>
/// Command to edit an existing score entry for a player in a live session.
/// Uses the same domain method as RecordScore (upsert behavior).
/// Issue #4749: CQRS commands for live sessions.
/// </summary>
internal record EditLiveSessionScoreCommand(
    Guid SessionId,
    Guid PlayerId,
    int Round,
    string Dimension,
    int Value,
    string? Unit = null
) : ICommand;
