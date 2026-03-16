using Api.BoundedContexts.KnowledgeBase.Domain.Events;
using Api.BoundedContexts.UserNotifications.Application.Services;
using Api.BoundedContexts.UserNotifications.Domain.ValueObjects;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.UserNotifications.Application.EventHandlers;

/// <summary>
/// Handles AgentAutoCreatedEvent to notify the user that an AI agent has been
/// automatically created for their private game after PDF indexing completed.
///
/// Game Night Improvvisata: After a user uploads a rulebook PDF and it finishes
/// indexing, we auto-create an agent and then inform them via in-app notification
/// so they can start using the AI assistant immediately.
/// </summary>
internal sealed class NotifyAgentReadyHandler : INotificationHandler<AgentAutoCreatedEvent>
{
    private readonly INotificationDispatcher _dispatcher;
    private readonly ILogger<NotifyAgentReadyHandler> _logger;

    public NotifyAgentReadyHandler(
        INotificationDispatcher dispatcher,
        ILogger<NotifyAgentReadyHandler> logger)
    {
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task Handle(AgentAutoCreatedEvent notification, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(notification);

        try
        {
            var gameName = notification.GameName;
            var deepLink = $"/library/private/{notification.PrivateGameId}/toolkit";

            await _dispatcher.DispatchAsync(new NotificationMessage
            {
                Type = NotificationType.AgentAutoCreated,
                RecipientUserId = notification.UserId,
                Payload = new GenericPayload(
                    $"Agente per {gameName} pronto!",
                    $"L'agente AI per {gameName} è stato creato automaticamente."),
                DeepLinkPath = deepLink
            }, cancellationToken).ConfigureAwait(false);

            _logger.LogInformation(
                "NotifyAgentReadyHandler: Dispatched AgentAutoCreated notification for UserId={UserId}, " +
                "AgentId={AgentId}, GameId={GameId}, GameName={GameName}",
                notification.UserId,
                notification.AgentDefinitionId,
                notification.PrivateGameId,
                gameName);
        }
#pragma warning disable CA1031
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "NotifyAgentReadyHandler: Failed to dispatch notification for UserId={UserId}, " +
                "AgentId={AgentId}, GameId={GameId}. Notification skipped.",
                notification.UserId,
                notification.AgentDefinitionId,
                notification.PrivateGameId);
            // Do not rethrow — notification failure must not block the event pipeline.
        }
#pragma warning restore CA1031
    }
}
