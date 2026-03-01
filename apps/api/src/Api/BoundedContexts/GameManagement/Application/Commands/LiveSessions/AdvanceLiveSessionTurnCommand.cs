using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.GameManagement.Application.Commands.LiveSessions;

/// <summary>
/// Command to advance to the next turn in a live game session.
/// Issue #4749: CQRS commands for live sessions.
/// </summary>
internal record AdvanceLiveSessionTurnCommand(Guid SessionId) : ICommand;
