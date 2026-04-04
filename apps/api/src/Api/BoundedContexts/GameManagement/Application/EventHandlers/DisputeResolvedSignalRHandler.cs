using Api.BoundedContexts.GameManagement.Domain.Events;
using Api.Hubs;
using MediatR;
using Microsoft.AspNetCore.SignalR;

namespace Api.BoundedContexts.GameManagement.Application.EventHandlers;

/// <summary>
/// Broadcasts the dispute resolution result to all clients in the session
/// via SignalR when a <see cref="DisputeResolvedEvent"/> is raised.
/// Issue: Game Night Improvvisata — real-time arbitro verdict push.
/// </summary>
internal sealed class DisputeResolvedSignalRHandler : INotificationHandler<DisputeResolvedEvent>
{
    private readonly IHubContext<GameStateHub> _hubContext;
    private readonly ILogger<DisputeResolvedSignalRHandler> _logger;

    public DisputeResolvedSignalRHandler(
        IHubContext<GameStateHub> hubContext,
        ILogger<DisputeResolvedSignalRHandler> logger)
    {
        _hubContext = hubContext ?? throw new ArgumentNullException(nameof(hubContext));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task Handle(DisputeResolvedEvent notification, CancellationToken cancellationToken)
    {
        var group = $"session:{notification.SessionId}";

        await _hubContext.Clients.Group(group)
            .SendAsync("DisputeResolved", new
            {
                notification.Dispute.Id,
                notification.Dispute.Description,
                notification.Dispute.Verdict,
                notification.Dispute.RuleReferences,
                notification.Dispute.RaisedByPlayerName,
                notification.Dispute.Timestamp
            }, cancellationToken)
            .ConfigureAwait(false);

        _logger.LogInformation(
            "DisputeResolved SignalR broadcast sent for session {SessionId}, dispute {DisputeId}",
            notification.SessionId,
            notification.Dispute.Id);
    }
}
