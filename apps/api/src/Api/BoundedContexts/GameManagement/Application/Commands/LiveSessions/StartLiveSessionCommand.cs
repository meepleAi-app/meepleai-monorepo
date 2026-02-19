using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.GameManagement.Application.Commands.LiveSessions;

/// <summary>
/// Command to start a live game session.
/// Issue #4749: CQRS commands for live sessions.
/// </summary>
internal record StartLiveSessionCommand(Guid SessionId) : ICommand;
