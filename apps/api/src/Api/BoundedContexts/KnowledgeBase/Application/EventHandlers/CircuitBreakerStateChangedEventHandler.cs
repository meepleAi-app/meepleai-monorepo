using Api.BoundedContexts.KnowledgeBase.Domain.Events;
using Api.BoundedContexts.KnowledgeBase.Domain.Services;
using Api.BoundedContexts.UserNotifications.Domain.Aggregates;
using Api.BoundedContexts.UserNotifications.Domain.Repositories;
using Api.BoundedContexts.UserNotifications.Domain.ValueObjects;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.KnowledgeBase.Application.EventHandlers;

/// <summary>
/// ISSUE-5086: Creates admin notifications when an LLM provider circuit breaker changes state.
/// Handles CircuitBreakerStateChangedEvent published fire-and-forget by HybridLlmService.
/// </summary>
internal sealed class CircuitBreakerStateChangedEventHandler
    : INotificationHandler<CircuitBreakerStateChangedEvent>
{
    private readonly INotificationRepository _notificationRepository;
    private readonly MeepleAiDbContext _dbContext;
    private readonly ILogger<CircuitBreakerStateChangedEventHandler> _logger;

    public CircuitBreakerStateChangedEventHandler(
        INotificationRepository notificationRepository,
        MeepleAiDbContext dbContext,
        ILogger<CircuitBreakerStateChangedEventHandler> logger)
    {
        _notificationRepository = notificationRepository
            ?? throw new ArgumentNullException(nameof(notificationRepository));
        _dbContext = dbContext
            ?? throw new ArgumentNullException(nameof(dbContext));
        _logger = logger
            ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task Handle(
        CircuitBreakerStateChangedEvent notification,
        CancellationToken cancellationToken)
    {
        try
        {
            var adminIds = await _dbContext.Set<UserEntity>()
                .AsNoTracking()
                .Where(u => u.Role == "admin")
                .Select(u => u.Id)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            if (adminIds.Count == 0)
            {
                _logger.LogDebug(
                    "CircuitBreakerStateChangedEvent: no admin users to notify (provider={Provider})",
                    notification.Provider);
                return;
            }

            var (severity, title) = notification.NewState switch
            {
                CircuitState.Open =>
                    (NotificationSeverity.Error, $"Circuit breaker OPENED — {notification.Provider}"),
                CircuitState.HalfOpen =>
                    (NotificationSeverity.Warning, $"Circuit breaker testing recovery — {notification.Provider}"),
                CircuitState.Closed =>
                    (NotificationSeverity.Success, $"Circuit breaker recovered — {notification.Provider}"),
                _ =>
                    (NotificationSeverity.Info, $"Circuit breaker state change — {notification.Provider}")
            };

            var metadata = System.Text.Json.JsonSerializer.Serialize(new
            {
                provider = notification.Provider,
                previousState = notification.PreviousState.ToString(),
                newState = notification.NewState.ToString(),
                occurredAt = notification.OccurredAt
            });

            foreach (var adminId in adminIds)
            {
                var n = new Notification(
                    id: Guid.NewGuid(),
                    userId: adminId,
                    type: NotificationType.AdminCircuitBreakerStateChanged,
                    severity: severity,
                    title: title,
                    message: notification.Reason,
                    link: "/admin/agents/usage",
                    metadata: metadata);

                await _notificationRepository.AddAsync(n, cancellationToken).ConfigureAwait(false);
            }

            await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            _logger.LogInformation(
                "CircuitBreakerStateChangedEvent: notified {Count} admins — {Provider} {Prev}→{New}",
                adminIds.Count, notification.Provider,
                notification.PreviousState, notification.NewState);
        }
#pragma warning disable CA1031
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to handle CircuitBreakerStateChangedEvent for {Provider}",
                notification.Provider);
            // Don't rethrow — handler failures must not affect the caller
        }
#pragma warning restore CA1031
    }
}
