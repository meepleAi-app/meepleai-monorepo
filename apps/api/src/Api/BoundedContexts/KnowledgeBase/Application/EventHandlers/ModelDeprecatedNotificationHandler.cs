using System.Text.Json;
using Api.BoundedContexts.KnowledgeBase.Domain.Events;
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
/// ISSUE-5499: Creates admin notifications when an LLM model is detected as deprecated/unavailable.
/// Handles ModelDeprecatedEvent published by ModelAvailabilityCheckJob.
/// Part of Epic #5490 - Model Versioning &amp; Availability Monitoring.
/// </summary>
internal sealed class ModelDeprecatedNotificationHandler
    : INotificationHandler<ModelDeprecatedEvent>
{
    private readonly INotificationRepository _notificationRepository;
    private readonly MeepleAiDbContext _dbContext;
    private readonly ILogger<ModelDeprecatedNotificationHandler> _logger;

    public ModelDeprecatedNotificationHandler(
        INotificationRepository notificationRepository,
        MeepleAiDbContext dbContext,
        ILogger<ModelDeprecatedNotificationHandler> logger)
    {
        _notificationRepository = notificationRepository
            ?? throw new ArgumentNullException(nameof(notificationRepository));
        _dbContext = dbContext
            ?? throw new ArgumentNullException(nameof(dbContext));
        _logger = logger
            ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task Handle(
        ModelDeprecatedEvent notification,
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
                    "ModelDeprecatedEvent: no admin users to notify (model={ModelId})",
                    notification.ModelId);
                return;
            }

            var strategiesText = string.Join(", ", notification.AffectedStrategies);
            var replacementText = notification.SuggestedReplacement ?? "none available";

            var title = $"LLM Model Unavailable: {notification.ModelId}";

            var message = $"Model '{notification.ModelId}' is no longer available on {notification.Provider}. " +
                          $"Affected strategies: {strategiesText}. " +
                          $"Suggested replacement: {replacementText}.";

            var metadata = JsonSerializer.Serialize(new
            {
                modelId = notification.ModelId,
                provider = notification.Provider,
                affectedStrategies = notification.AffectedStrategies,
                suggestedReplacement = notification.SuggestedReplacement,
                reason = notification.Reason,
                detectedAt = notification.DetectedAt,
            });

            foreach (var adminId in adminIds)
            {
                var n = new Notification(
                    id: Guid.NewGuid(),
                    userId: adminId,
                    type: NotificationType.AdminModelDeprecated,
                    severity: NotificationSeverity.Warning,
                    title: title,
                    message: message,
                    link: "/admin/agents/usage",
                    metadata: metadata);

                await _notificationRepository.AddAsync(n, cancellationToken).ConfigureAwait(false);
            }

            await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            _logger.LogInformation(
                "ModelDeprecatedEvent: notified {Count} admins — {ModelId} affects [{Strategies}], replacement: {Replacement}",
                adminIds.Count, notification.ModelId, strategiesText, replacementText);
        }
#pragma warning disable CA1031
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to handle ModelDeprecatedEvent for {ModelId}",
                notification.ModelId);
            // Don't rethrow — handler failures must not affect the caller
        }
#pragma warning restore CA1031
    }
}
