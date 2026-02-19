using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.GameManagement.Application.Commands.LiveSessions;

/// <summary>
/// Command to update notes on a live game session.
/// Issue #4749: CQRS commands for live sessions.
/// </summary>
internal record UpdateLiveSessionNotesCommand(
    Guid SessionId,
    string? Notes
) : ICommand;
