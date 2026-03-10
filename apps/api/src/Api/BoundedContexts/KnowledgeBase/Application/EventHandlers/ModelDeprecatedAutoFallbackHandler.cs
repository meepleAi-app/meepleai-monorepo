using System.Text.Json;
using Api.BoundedContexts.KnowledgeBase.Domain.Events;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.BoundedContexts.UserNotifications.Domain.Aggregates;
using Api.BoundedContexts.UserNotifications.Domain.Repositories;
using Api.BoundedContexts.UserNotifications.Domain.ValueObjects;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Infrastructure.Entities.KnowledgeBase;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.KnowledgeBase.Application.EventHandlers;

/// <summary>
/// ISSUE-5501: Auto-fallback when a configured model is deprecated.
/// When ModelDeprecatedEvent fires, checks affected strategy mappings for configured
/// fallback models and automatically switches to the first available one.
/// Part of Epic #5490 - Model Versioning &amp; Availability Monitoring.
/// </summary>
internal sealed class ModelDeprecatedAutoFallbackHandler
    : INotificationHandler<ModelDeprecatedEvent>
{
    private readonly MeepleAiDbContext _dbContext;
    private readonly IModelCompatibilityRepository _compatibilityRepository;
    private readonly INotificationRepository _notificationRepository;
    private readonly ILogger<ModelDeprecatedAutoFallbackHandler> _logger;

    public ModelDeprecatedAutoFallbackHandler(
        MeepleAiDbContext dbContext,
        IModelCompatibilityRepository compatibilityRepository,
        INotificationRepository notificationRepository,
        ILogger<ModelDeprecatedAutoFallbackHandler> logger)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _compatibilityRepository = compatibilityRepository
            ?? throw new ArgumentNullException(nameof(compatibilityRepository));
        _notificationRepository = notificationRepository
            ?? throw new ArgumentNullException(nameof(notificationRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task Handle(
        ModelDeprecatedEvent notification,
        CancellationToken cancellationToken)
    {
        try
        {
            // Find strategy mappings that use the deprecated model as primary AND have fallbacks
            var affectedMappings = await _dbContext.Set<StrategyModelMappingEntity>()
                .Where(m => m.PrimaryModel == notification.ModelId
                    && notification.AffectedStrategies.Contains(m.Strategy)
                    && m.FallbackModels != null
                    && m.FallbackModels.Length > 0)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            if (affectedMappings.Count == 0)
            {
                _logger.LogDebug(
                    "ModelDeprecatedAutoFallback: no strategies with fallbacks found for {ModelId}",
                    notification.ModelId);
                return;
            }

            var switchedStrategies = new List<(string Strategy, string OldModel, string NewModel)>();

            foreach (var mapping in affectedMappings)
            {
                var fallbackModel = mapping.FallbackModels[0];
                var previousModel = mapping.PrimaryModel;

                mapping.PrimaryModel = fallbackModel;
                mapping.FallbackModels = mapping.FallbackModels.Skip(1).ToArray();
                mapping.UpdatedAt = DateTime.UtcNow;

                switchedStrategies.Add((mapping.Strategy, previousModel, fallbackModel));

                _logger.LogWarning(
                    "Auto-fallback: strategy {Strategy} switched from {OldModel} to {NewModel}",
                    mapping.Strategy, previousModel, fallbackModel);

                // Log each switch in the audit trail
                await _compatibilityRepository.LogChangeAsync(
                    new ModelChangeLogEntry(
                        Guid.NewGuid(),
                        notification.ModelId,
                        "fallback_activated",
                        previousModel,
                        fallbackModel,
                        mapping.Strategy,
                        $"Auto-fallback activated: {previousModel} → {fallbackModel} (reason: {notification.Reason})",
                        true,
                        null,
                        DateTime.UtcNow),
                    cancellationToken).ConfigureAwait(false);
            }

            // Notify admins about the automatic switch
            var adminIds = await _dbContext.Set<UserEntity>()
                .AsNoTracking()
                .Where(u => u.Role == "admin")
                .Select(u => u.Id)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            if (adminIds.Count > 0)
            {
                var strategySummary = string.Join(", ",
                    switchedStrategies.Select(s => $"{s.Strategy}: {s.OldModel} → {s.NewModel}"));

                var title = $"Auto-Fallback Activated: {notification.ModelId}";
                var message = $"Model '{notification.ModelId}' is deprecated. " +
                              $"Automatic fallback applied to {switchedStrategies.Count} " +
                              $"strateg{(switchedStrategies.Count == 1 ? "y" : "ies")}: {strategySummary}. " +
                              $"Use Model Configuration to revert or choose a different model.";

                var metadata = JsonSerializer.Serialize(new
                {
                    modelId = notification.ModelId,
                    provider = notification.Provider,
                    isAutomatic = true,
                    switchedStrategies = switchedStrategies.Select(s => new
                    {
                        strategy = s.Strategy,
                        oldModel = s.OldModel,
                        newModel = s.NewModel,
                    }).ToArray(),
                });

                foreach (var adminId in adminIds)
                {
                    var n = new Notification(
                        id: Guid.NewGuid(),
                        userId: adminId,
                        type: NotificationType.AdminModelAutoFallback,
                        severity: NotificationSeverity.Warning,
                        title: title,
                        message: message,
                        link: "/admin/agents/strategy",
                        metadata: metadata);

                    await _notificationRepository.AddAsync(n, cancellationToken).ConfigureAwait(false);
                }
            }

            await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            _logger.LogInformation(
                "ModelDeprecatedAutoFallback: switched {Count} strategies for {ModelId}, notified {AdminCount} admins",
                switchedStrategies.Count, notification.ModelId, adminIds.Count);
        }
#pragma warning disable CA1031
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to handle auto-fallback for deprecated model {ModelId}",
                notification.ModelId);
            // Don't rethrow — handler failures must not affect the caller
        }
#pragma warning restore CA1031
    }
}
