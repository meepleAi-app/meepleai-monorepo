using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.GameManagement.Application.Commands.LiveSessions;

/// <summary>
/// Command to complete a live game session.
/// Triggers PlayRecord generation via domain events.
/// Issue #4749: CQRS commands for live sessions.
/// </summary>
internal record CompleteLiveSessionCommand(Guid SessionId) : ICommand;
