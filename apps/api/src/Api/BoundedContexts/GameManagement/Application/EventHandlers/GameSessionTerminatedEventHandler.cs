using Api.BoundedContexts.GameManagement.Domain.Events;
using Api.BoundedContexts.UserNotifications.Application.Constants;
using Api.BoundedContexts.UserNotifications.Domain.Aggregates;
using Api.BoundedContexts.UserNotifications.Domain.Repositories;
using Api.BoundedContexts.UserNotifications.Domain.ValueObjects;
using Api.Infrastructure;
using Api.SharedKernel.Application.EventHandlers;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.GameManagement.Application.EventHandlers;

/// <summary>
/// Event handler for GameSessionTerminatedEvent.
/// Issue #3671: Creates notification when session auto-terminated for quota enforcement.
/// </summary>
internal sealed class GameSessionTerminatedEventHandler : DomainEventHandlerBase<GameSessionTerminatedEvent>
{
    private readonly INotificationRepository _notificationRepository;

    public GameSessionTerminatedEventHandler(
        MeepleAiDbContext dbContext,
        ILogger<GameSessionTerminatedEventHandler> logger,
        INotificationRepository notificationRepository)
        : base(dbContext, logger)
    {
        _notificationRepository = notificationRepository ?? throw new ArgumentNullException(nameof(notificationRepository));
    }

    protected override async Task HandleEventAsync(GameSessionTerminatedEvent domainEvent, CancellationToken cancellationToken)
    {
        try
        {
            // Auto-audit logging is handled by base class

            // Send notification only for quota-based terminations
            if (string.Equals(domainEvent.TerminationReason, "QuotaExceeded", StringComparison.Ordinal))
            {
                var notification = new Notification(
                    id: Guid.NewGuid(),
                    userId: domainEvent.UserId,
                    type: NotificationType.SessionTerminated,
                    severity: NotificationSeverity.Warning,
                    title: "Session Automatically Closed",
                    message: "One of your game sessions was automatically closed because you reached your session limit. " +
                             "Complete or abandon existing sessions before starting new ones, or upgrade your subscription for higher limits.",
                    link: NotificationRoutes.Sessions,
                    metadata: System.Text.Json.JsonSerializer.Serialize(new
                    {
                        sessionId = domainEvent.SessionId,
                        terminationReason = domainEvent.TerminationReason,
                        terminatedAt = domainEvent.TerminatedAt
                    }));

                // Issue #2392: AddAndCommitAsync commits the notification row first,
                // then fires the metric + SSE broadcast. The main GameSession transaction
                // is already committed by the time this MediatR handler runs (Hybrid /
                // OutboxOnly dispatch — see MeepleAiDbContext.SaveChangesAsyncCore step 5),
                // so a separate notification commit can't orphan the parent. Note: under
                // Hybrid mode the handler still shares the request-scoped DbContext, so
                // upstream tracked-but-unsaved entities would commit alongside the
                // notification — callers in the pipeline are expected to flush their own
                // mutations before raising the GameSessionTerminatedEvent.
                await _notificationRepository.AddAndCommitAsync(notification, cancellationToken)
                    .ConfigureAwait(false);

                Logger.LogInformation(
                    "Created session termination notification for user {UserId}, session {SessionId}",
                    domainEvent.UserId,
                    domainEvent.SessionId);
            }
        }
#pragma warning disable CA1031 // Do not catch general exception types
        // SERVICE BOUNDARY: EVENT HANDLER PATTERN - Background event processing
        // Event handlers must not throw exceptions (violates mediator/event pattern).
        // Errors logged for monitoring; notification failures don't block session termination.
        catch (Exception ex)
        {
            Logger.LogError(ex,
                "Failed to create session termination notification for user {UserId}, session {SessionId}",
                domainEvent.UserId,
                domainEvent.SessionId);
            // Don't rethrow - notification failure should not block session termination
        }
#pragma warning restore CA1031
    }

    protected override Dictionary<string, object?>? GetAuditMetadata(GameSessionTerminatedEvent domainEvent)
    {
        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["SessionId"] = domainEvent.SessionId,
            ["UserId"] = domainEvent.UserId,
            ["TerminationReason"] = domainEvent.TerminationReason,
            ["TerminatedAt"] = domainEvent.TerminatedAt,
            ["Action"] = "GameSessionTerminated"
        };
    }
}
