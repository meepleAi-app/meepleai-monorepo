using Api.BoundedContexts.GameManagement.Application.Services;
using Api.BoundedContexts.GameManagement.Domain.Events;
using MediatR;

namespace Api.BoundedContexts.GameManagement.Application.EventHandlers;

/// <summary>
/// Translates LiveGameSession domain events into canonical SSE stream events
/// and broadcasts them via <see cref="ILiveSessionStreamGateway"/> (ADR-083 SP2 Task 5).
/// </summary>
internal sealed class LiveSessionStreamForwarder :
    INotificationHandler<LiveSessionScoreRecordedEvent>,
    INotificationHandler<LiveSessionTurnAdvancedEvent>,
    INotificationHandler<LiveSessionPhaseAdvancedEvent>,
    INotificationHandler<LiveSessionPlayerAddedEvent>,
    INotificationHandler<LiveSessionPlayerRemovedEvent>,
    INotificationHandler<LiveSessionPausedEvent>,
    INotificationHandler<LiveSessionResumedEvent>,
    INotificationHandler<LiveSessionCompletedEvent>
{
    private readonly ILiveSessionStreamGateway _gateway;
    private readonly ILogger<LiveSessionStreamForwarder> _logger;

    public LiveSessionStreamForwarder(
        ILiveSessionStreamGateway gateway,
        ILogger<LiveSessionStreamForwarder> logger)
    {
        _gateway = gateway ?? throw new ArgumentNullException(nameof(gateway));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public Task Handle(LiveSessionScoreRecordedEvent notification, CancellationToken cancellationToken)
    {
        _logger.LogDebug("Broadcasting session:score for session {SessionId}", notification.SessionId);
        return _gateway.BroadcastAsync(
            notification.SessionId,
            new LiveSessionStreamEvent("session:score", new
            {
                notification.PlayerId,
                notification.Round,
                notification.Dimension,
                notification.Value
            }),
            cancellationToken);
    }

    public Task Handle(LiveSessionTurnAdvancedEvent notification, CancellationToken cancellationToken)
    {
        _logger.LogDebug("Broadcasting session:turn for session {SessionId}", notification.SessionId);
        return _gateway.BroadcastAsync(
            notification.SessionId,
            new LiveSessionStreamEvent("session:turn", new
            {
                notification.NewTurnIndex,
                notification.CurrentPlayerId
            }),
            cancellationToken);
    }

    public Task Handle(LiveSessionPhaseAdvancedEvent notification, CancellationToken cancellationToken)
    {
        _logger.LogDebug("Broadcasting session:phase for session {SessionId}", notification.SessionId);
        return _gateway.BroadcastAsync(
            notification.SessionId,
            new LiveSessionStreamEvent("session:phase", new
            {
                notification.TurnIndex,
                notification.NewPhaseIndex,
                notification.PhaseName,
                notification.TotalPhases
            }),
            cancellationToken);
    }

    public Task Handle(LiveSessionPlayerAddedEvent notification, CancellationToken cancellationToken)
    {
        _logger.LogDebug("Broadcasting session:player-join for session {SessionId}", notification.SessionId);
        return _gateway.BroadcastAsync(
            notification.SessionId,
            new LiveSessionStreamEvent("session:player-join", new
            {
                notification.PlayerId,
                notification.UserId,
                notification.DisplayName,
                Role = notification.Role.ToString()
            }),
            cancellationToken);
    }

    public Task Handle(LiveSessionPlayerRemovedEvent notification, CancellationToken cancellationToken)
    {
        _logger.LogDebug("Broadcasting session:player-leave for session {SessionId}", notification.SessionId);
        return _gateway.BroadcastAsync(
            notification.SessionId,
            new LiveSessionStreamEvent("session:player-leave", new
            {
                notification.PlayerId
            }),
            cancellationToken);
    }

    public Task Handle(LiveSessionPausedEvent notification, CancellationToken cancellationToken)
    {
        _logger.LogDebug("Broadcasting session:pause for session {SessionId}", notification.SessionId);
        return _gateway.BroadcastAsync(
            notification.SessionId,
            new LiveSessionStreamEvent("session:pause", new
            {
                notification.PausedAt
            }),
            cancellationToken);
    }

    public Task Handle(LiveSessionResumedEvent notification, CancellationToken cancellationToken)
    {
        _logger.LogDebug("Broadcasting session:resume for session {SessionId}", notification.SessionId);
        return _gateway.BroadcastAsync(
            notification.SessionId,
            new LiveSessionStreamEvent("session:resume", new
            {
                notification.ResumedAt
            }),
            cancellationToken);
    }

    public Task Handle(LiveSessionCompletedEvent notification, CancellationToken cancellationToken)
    {
        _logger.LogDebug("Broadcasting session:endgame for session {SessionId}", notification.SessionId);
        return _gateway.BroadcastAsync(
            notification.SessionId,
            new LiveSessionStreamEvent("session:endgame", new
            {
                notification.CompletedAt,
                notification.TotalTurns,
                notification.GameName
            }),
            cancellationToken);
    }
}
