using Api.BoundedContexts.SharedGameCatalog.Domain.Events;
using Api.BoundedContexts.UserNotifications.Application.Services;
using Api.BoundedContexts.UserNotifications.Domain.ValueObjects;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Infrastructure.Entities.SharedGameCatalog;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.UserNotifications.Application.EventHandlers;

/// <summary>
/// Event handler that creates admin notifications when an AI agent is linked to a shared game.
/// Dispatches via NotificationDispatcher for multi-channel delivery (in-app, email, Slack).
/// Issue #5009: Notification triggers for admin workflow (PDF ready + agent linked).
/// </summary>
internal sealed class AgentLinkedNotificationEventHandler : INotificationHandler<AgentLinkedToSharedGameEvent>
{
    private readonly INotificationDispatcher _dispatcher;
    private readonly MeepleAiDbContext _dbContext;
    private readonly ILogger<AgentLinkedNotificationEventHandler> _logger;

    public AgentLinkedNotificationEventHandler(
        INotificationDispatcher dispatcher,
        MeepleAiDbContext dbContext,
        ILogger<AgentLinkedNotificationEventHandler> logger)
    {
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task Handle(AgentLinkedToSharedGameEvent notification, CancellationToken cancellationToken)
    {
        try
        {
            // Resolve game title for a meaningful notification message
            var gameTitle = await _dbContext.Set<SharedGameEntity>()
                .AsNoTracking()
                .Where(g => g.Id == notification.GameId)
                .Select(g => g.Title)
                .FirstOrDefaultAsync(cancellationToken)
                .ConfigureAwait(false) ?? notification.GameId.ToString();

            // Notify all admin users
            var adminIds = await _dbContext.Set<UserEntity>()
                .AsNoTracking()
                .Where(u => u.Role == "admin")
                .Select(u => u.Id)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            if (adminIds.Count == 0)
            {
                _logger.LogWarning(
                    "No admin users found to notify for agent {AgentId} linked to game {GameId}",
                    notification.AgentDefinitionId, notification.GameId);
                return;
            }

            foreach (var adminId in adminIds)
            {
                await _dispatcher.DispatchAsync(new NotificationMessage
                {
                    Type = NotificationType.AgentLinked,
                    RecipientUserId = adminId,
                    Payload = new GenericPayload(
                        "Agent Linked",
                        $"Agent linked to '{gameTitle}'. Game is ready for AI chat."),
                    DeepLinkPath = $"/admin/shared-games/{notification.GameId}"
                }, cancellationToken).ConfigureAwait(false);
            }

            _logger.LogInformation(
                "Dispatched {Count} admin notifications for agent {AgentId} linked to game {GameId}",
                adminIds.Count, notification.AgentDefinitionId, notification.GameId);
        }
#pragma warning disable CA1031
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to create admin notifications for agent {AgentId} linked to game {GameId}",
                notification.AgentDefinitionId, notification.GameId);
            // Don't rethrow - event handler failures must not block the main operation
        }
#pragma warning restore CA1031
    }
}
