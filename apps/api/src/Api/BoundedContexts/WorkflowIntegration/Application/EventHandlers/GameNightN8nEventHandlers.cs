using Api.BoundedContexts.GameManagement.Domain.Events;
using Api.BoundedContexts.WorkflowIntegration.Application.Services;
using MediatR;

namespace Api.BoundedContexts.WorkflowIntegration.Application.EventHandlers;

/// <summary>
/// Forwards game night domain events to n8n workflows via webhook.
/// Issue #58: API → n8n trigger for calendar events.
/// Fire-and-forget: n8n failures don't block the main flow.
/// </summary>
internal sealed class GameNightPublishedN8nHandler : INotificationHandler<GameNightPublishedEvent>
{
    private readonly IN8nWebhookClient _n8nClient;
    private readonly ILogger<GameNightPublishedN8nHandler> _logger;

    public GameNightPublishedN8nHandler(IN8nWebhookClient n8nClient, ILogger<GameNightPublishedN8nHandler> logger)
    {
        _n8nClient = n8nClient;
        _logger = logger;
    }

    public async Task Handle(GameNightPublishedEvent notification, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Forwarding GameNightPublished event {EventId} to n8n", notification.GameNightEventId);

        await _n8nClient.TriggerWorkflowAsync("game-night-published", new
        {
            eventId = notification.GameNightEventId,
            organizerId = notification.OrganizerId,
            title = notification.Title,
            scheduledAt = notification.ScheduledAt,
            invitedUserIds = notification.InvitedUserIds
        }, cancellationToken).ConfigureAwait(false);
    }
}

internal sealed class GameNightCancelledN8nHandler : INotificationHandler<GameNightCancelledEvent>
{
    private readonly IN8nWebhookClient _n8nClient;
    private readonly ILogger<GameNightCancelledN8nHandler> _logger;

    public GameNightCancelledN8nHandler(IN8nWebhookClient n8nClient, ILogger<GameNightCancelledN8nHandler> logger)
    {
        _n8nClient = n8nClient;
        _logger = logger;
    }

    public async Task Handle(GameNightCancelledEvent notification, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Forwarding GameNightCancelled event {EventId} to n8n", notification.GameNightEventId);

        await _n8nClient.TriggerWorkflowAsync("game-night-cancelled", new
        {
            eventId = notification.GameNightEventId,
            organizerId = notification.OrganizerId,
            title = notification.Title,
            invitedUserIds = notification.InvitedUserIds
        }, cancellationToken).ConfigureAwait(false);
    }
}

internal sealed class GameNightRsvpN8nHandler : INotificationHandler<GameNightRsvpReceivedEvent>
{
    private readonly IN8nWebhookClient _n8nClient;
    private readonly ILogger<GameNightRsvpN8nHandler> _logger;

    public GameNightRsvpN8nHandler(IN8nWebhookClient n8nClient, ILogger<GameNightRsvpN8nHandler> logger)
    {
        _n8nClient = n8nClient;
        _logger = logger;
    }

    public async Task Handle(GameNightRsvpReceivedEvent notification, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Forwarding GameNightRsvp event {EventId} to n8n", notification.GameNightEventId);

        await _n8nClient.TriggerWorkflowAsync("game-night-rsvp-changed", new
        {
            eventId = notification.GameNightEventId,
            userId = notification.UserId,
            rsvpStatus = notification.RsvpStatus.ToString(),
            organizerId = notification.OrganizerId
        }, cancellationToken).ConfigureAwait(false);
    }
}
