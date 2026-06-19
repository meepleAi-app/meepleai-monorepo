using Api.BoundedContexts.SessionTracking.Domain.Events;
using Api.Hubs;
using MediatR;
using Microsoft.AspNetCore.SignalR;

namespace Api.BoundedContexts.SessionTracking.Application.EventHandlers;

/// <summary>
/// Broadcasts a <c>ScoringConfigured</c> SignalR event to the session group
/// every time <see cref="SessionScoresUpdatedEvent"/> is raised. Lets live
/// clients re-sync their polymorphic scoring store on host writes (#2389
/// Block A). Sender of the original write is identified via <see cref="SessionScoresUpdatedEvent.SessionId"/>
/// — clients filter their own optimistic updates by session.
/// </summary>
public class SessionScoresUpdatedSignalRHandler : INotificationHandler<SessionScoresUpdatedEvent>
{
    private readonly IHubContext<GameStateHub> _hubContext;
    private readonly ILogger<SessionScoresUpdatedSignalRHandler> _logger;

    public SessionScoresUpdatedSignalRHandler(
        IHubContext<GameStateHub> hubContext,
        ILogger<SessionScoresUpdatedSignalRHandler> logger)
    {
        _hubContext = hubContext ?? throw new ArgumentNullException(nameof(hubContext));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task Handle(SessionScoresUpdatedEvent notification, CancellationToken cancellationToken)
    {
        var payload = new
        {
            sessionId = notification.SessionId,
            scoringType = notification.ScoringType.ToString(),
            scoreData = notification.ScoreData,
        };

        await _hubContext.Clients
            .Group($"session:{notification.SessionId}")
            .SendAsync("ScoringConfigured", payload, cancellationToken)
            .ConfigureAwait(false);

        _logger.LogDebug(
            "ScoringConfigured broadcast for session {SessionId} (scoringType={ScoringType})",
            notification.SessionId,
            notification.ScoringType);
    }
}
