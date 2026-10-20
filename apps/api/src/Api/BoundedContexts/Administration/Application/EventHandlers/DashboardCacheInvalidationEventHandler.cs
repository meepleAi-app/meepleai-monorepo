using Api.BoundedContexts.SystemConfiguration.Domain.Events;
using Api.Observability;
using Api.Services;
using MediatR;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.Administration.Application.EventHandlers;

/// <summary>
/// Issue #879: Invalidates dashboard cache when system configuration changes.
/// Ensures dashboard stats reflect configuration updates within 1 minute (or immediately on config change).
/// </summary>
internal sealed class DashboardCacheInvalidationEventHandler :
    INotificationHandler<ConfigurationUpdatedEvent>,
    INotificationHandler<ConfigurationToggledEvent>
{
    private readonly HybridCache _cache;
    private readonly ILogger<DashboardCacheInvalidationEventHandler> _logger;

    public DashboardCacheInvalidationEventHandler(
        HybridCache cache,
        ILogger<DashboardCacheInvalidationEventHandler> logger)
    {
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Handle ConfigurationUpdatedEvent - invalidate dashboard cache.
    /// </summary>
    public async Task Handle(ConfigurationUpdatedEvent notification, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(notification);
        _logger.LogInformation(
            "Configuration updated: {Key} → invalidating dashboard cache",
            notification.Key.Value);

        await InvalidateDashboardCacheAsync(notification.Key.Value, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Handle ConfigurationToggledEvent - invalidate dashboard cache.
    /// </summary>
    public async Task Handle(ConfigurationToggledEvent notification, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(notification);
        _logger.LogInformation(
            "Configuration toggled: {Key} (Active: {IsActive}) → invalidating dashboard cache",
            notification.Key.Value,
            notification.IsActive);

        await InvalidateDashboardCacheAsync(notification.Key.Value, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Invalidate all dashboard stats cache entries.
    /// </summary>
    /// <remarks>
    /// #4542 — per tag, non per chiave. La chiave di <see cref="AdminStatsService.GetDashboardStatsAsync"/>
    /// contiene l'intervallo di date, quindi le voci non si possono elencare. Prima di #4542 questo metodo
    /// rimuoveva sedici chiavi esatte (Days 7/14/30/90 per quattro ruoli, senza gioco): le voci con un
    /// gameId non venivano mai rimosse, e quelle con date esplicite solo perché, per il difetto, cadevano
    /// sulla chiave senza date. Il tag le raggiunge tutte, qualunque sia il formato della chiave.
    /// </remarks>
    private async Task InvalidateDashboardCacheAsync(string configKey, CancellationToken cancellationToken)
    {
        try
        {
            await _cache.RemoveByTagAsync(AdminStatsService.DashboardStatsCacheTag, cancellationToken)
                .ConfigureAwait(false);

            _logger.LogInformation(
                "Dashboard cache invalidated: tag {CacheTag} removed due to config change: {ConfigKey}",
                AdminStatsService.DashboardStatsCacheTag,
                configKey);

            // Issue #879: Record cache invalidation metric for Prometheus
            var tags = new System.Diagnostics.TagList
            {
                { "config_key", configKey }
            };
            MeepleAiMetrics.DashboardCacheInvalidationsTotal.Add(1, tags);
        }
#pragma warning disable CA1031 // Do not catch general exception types
#pragma warning disable S125 // Sections of code should not be commented out
        // SERVICE BOUNDARY: EVENT HANDLER PATTERN - Background event processing
        // Event handlers must not throw exceptions (violates mediator/event pattern).
        // Errors logged for monitoring; failed cache invalidation doesn't block config updates.
#pragma warning restore S125
        catch (Exception ex)
        {
            // Non-critical operation - log but don't throw
            _logger.LogError(ex,
                "Failed to invalidate dashboard cache after config change: {ConfigKey}",
                configKey);
        }
#pragma warning restore CA1031
    }
}
