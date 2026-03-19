using Api.BoundedContexts.GameManagement.Domain.Events;
using Api.Hubs;
using MediatR;
using Microsoft.AspNetCore.SignalR;

namespace Api.BoundedContexts.GameManagement.Application.Commands.GameNight;

/// <summary>
/// Broadcasts the structured dispute resolution result to all clients in the session
/// via SignalR when a <see cref="StructuredDisputeResolvedEvent"/> is raised.
/// Game Night Improvvisata v2 — real-time structured dispute resolution push.
/// </summary>
internal sealed class StructuredDisputeResolvedSignalRHandler : INotificationHandler<StructuredDisputeResolvedEvent>
{
    private readonly IHubContext<GameStateHub> _hubContext;
    private readonly ILogger<StructuredDisputeResolvedSignalRHandler> _logger;

    public StructuredDisputeResolvedSignalRHandler(
        IHubContext<GameStateHub> hubContext,
        ILogger<StructuredDisputeResolvedSignalRHandler> logger)
    {
        _hubContext = hubContext ?? throw new ArgumentNullException(nameof(hubContext));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task Handle(StructuredDisputeResolvedEvent notification, CancellationToken cancellationToken)
    {
        var group = $"session:{notification.SessionId}";

        await _hubContext.Clients.Group(group)
            .SendAsync("StructuredDisputeResolved", new
            {
                notification.DisputeId,
                notification.SessionId,
                notification.GameId,
                Verdict = new
                {
                    notification.Verdict.RulingFor,
                    notification.Verdict.Reasoning,
                    notification.Verdict.Citation,
                    notification.Verdict.Confidence
                },
                notification.FinalOutcome,
                notification.OverrideRule
            }, cancellationToken)
            .ConfigureAwait(false);

        _logger.LogInformation(
            "StructuredDisputeResolved SignalR broadcast sent for session {SessionId}, dispute {DisputeId}, outcome {Outcome}",
            notification.SessionId,
            notification.DisputeId,
            notification.FinalOutcome);
    }
}
