using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.GameManagement.Application.Commands.LiveSessions;

/// <summary>
/// Command to resume a paused live game session.
/// Issue #4749: CQRS commands for live sessions.
/// </summary>
internal record ResumeLiveSessionCommand(Guid SessionId) : ICommand;
