using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.GameManagement.Application.Commands.LiveSessions;

/// <summary>
/// Command to pause a live game session.
/// Issue #4749: CQRS commands for live sessions.
/// </summary>
internal record PauseLiveSessionCommand(Guid SessionId) : ICommand;
