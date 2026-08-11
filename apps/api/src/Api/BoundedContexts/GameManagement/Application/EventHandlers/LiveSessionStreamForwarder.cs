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
    INotificationHandler<LiveSessionCompletedEvent>,
    INotificationHandler<LiveSessionDiaryEntryAddedEvent>,
    INotificationHandler<LiveSessionGameStateEvent>
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

    public Task Handle(LiveSessionDiaryEntryAddedEvent notification, CancellationToken cancellationToken)
    {
        _logger.LogDebug("Broadcasting session:diary for session {SessionId}", notification.SessionId);
        return _gateway.BroadcastAsync(
            notification.SessionId,
            new LiveSessionStreamEvent("session:diary", new
            {
                entryId = notification.EntryId,
                authorId = notification.AuthorId,
                content = notification.Text,
                timestamp = notification.CreatedAt.ToString("o")
            }),
            cancellationToken);
    }

    public Task Handle(LiveSessionGameStateEvent notification, CancellationToken cancellationToken)
    {
        _logger.LogDebug("Broadcasting session:game-state for session {SessionId}", notification.SessionId);
        // The event carries the RAW JSON string (not the aggregate's disposable JsonDocument,
        // which System.Text.Json cannot serialize). Re-parse into a JsonNode (serializable, not
        // disposable) so `state` serializes as a nested OBJECT — not an escaped string.
        var stateNode = notification.RawStateJson is null
            ? null
            : System.Text.Json.Nodes.JsonNode.Parse(notification.RawStateJson);
        return _gateway.BroadcastAsync(
            notification.SessionId,
            new LiveSessionStreamEvent("session:game-state", new { state = stateNode }),
            cancellationToken);
    }
}
