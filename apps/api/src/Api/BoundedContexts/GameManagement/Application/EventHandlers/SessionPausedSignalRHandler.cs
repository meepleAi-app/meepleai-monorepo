using Api.BoundedContexts.GameManagement.Domain.Events;
using Api.Hubs;
using MediatR;
using Microsoft.AspNetCore.SignalR;

namespace Api.BoundedContexts.GameManagement.Application.EventHandlers;

/// <summary>
/// Notifies all clients in a session via SignalR that the session has been paused,
/// triggered by a <see cref="SessionPausedEvent"/> domain event.
/// Issue: Game Night Improvvisata — real-time pause push to participants.
/// </summary>
internal sealed class SessionPausedSignalRHandler : INotificationHandler<SessionPausedEvent>
{
    private readonly IHubContext<GameStateHub> _hubContext;
    private readonly ILogger<SessionPausedSignalRHandler> _logger;

    public SessionPausedSignalRHandler(
        IHubContext<GameStateHub> hubContext,
        ILogger<SessionPausedSignalRHandler> logger)
    {
        _hubContext = hubContext ?? throw new ArgumentNullException(nameof(hubContext));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task Handle(SessionPausedEvent notification, CancellationToken cancellationToken)
    {
        var group = $"session:{notification.SessionId}";

        await _hubContext.Clients.Group(group)
            .SendAsync("SessionPaused", cancellationToken)
            .ConfigureAwait(false);

        _logger.LogInformation(
            "SessionPaused SignalR broadcast sent for session {SessionId}",
            notification.SessionId);
    }
}
