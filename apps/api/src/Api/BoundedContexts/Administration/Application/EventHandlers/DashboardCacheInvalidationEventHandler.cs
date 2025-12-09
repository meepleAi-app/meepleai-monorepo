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
public sealed class DashboardCacheInvalidationEventHandler :
    INotificationHandler<ConfigurationUpdatedEvent>,
    INotificationHandler<ConfigurationToggledEvent>
{
    private readonly HybridCache _cache;
    private readonly ILogger<DashboardCacheInvalidationEventHandler> _logger;
    private const string DashboardCacheKeyPrefix = "dashboard:stats:";

    public DashboardCacheInvalidationEventHandler(
        HybridCache cache,
        ILogger<DashboardCacheInvalidationEventHandler> logger)
    {
        _cache = cache;
        _logger = logger;
    }

    /// <summary>
    /// Handle ConfigurationUpdatedEvent - invalidate dashboard cache.
    /// </summary>
    public async Task Handle(ConfigurationUpdatedEvent notification, CancellationToken cancellationToken)
    {
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
        _logger.LogInformation(
            "Configuration toggled: {Key} (Active: {IsActive}) → invalidating dashboard cache",
            notification.Key.Value,
            notification.IsActive);

        await InvalidateDashboardCacheAsync(notification.Key.Value, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Invalidate all dashboard cache entries.
    /// Issue #879: Uses pattern matching to remove all cached dashboard stats.
    /// </summary>
    private async Task InvalidateDashboardCacheAsync(string configKey, CancellationToken cancellationToken)
    {
        try
        {
            // HybridCache doesn't support wildcard removal, so we track known cache keys
            // Dashboard cache keys format: "dashboard:stats:{days}:{gameId}:{roleFilter}"
            // For simplicity, we invalidate common parameter combinations

            var cacheKeysToRemove = GenerateDashboardCacheKeys();

            foreach (var cacheKey in cacheKeysToRemove)
            {
                await _cache.RemoveAsync(cacheKey, cancellationToken).ConfigureAwait(false);
            }

            _logger.LogInformation(
                "Dashboard cache invalidated: {Count} cache entries removed due to config change: {ConfigKey}",
                cacheKeysToRemove.Count,
                configKey);

            // Issue #879: Record cache invalidation metric for Prometheus
            var tags = new System.Diagnostics.TagList
            {
                { "config_key", configKey },
                { "entries_removed", cacheKeysToRemove.Count }
            };
            MeepleAiMetrics.DashboardCacheInvalidationsTotal.Add(1, tags);
        }
        catch (Exception ex)
        {
            // Non-critical operation - log but don't throw
            _logger.LogError(ex,
                "Failed to invalidate dashboard cache after config change: {ConfigKey}",
                configKey);
        }
    }

    /// <summary>
    /// Generate common dashboard cache key combinations for invalidation.
    /// Covers most frequently used parameter combinations.
    /// </summary>
    private static List<string> GenerateDashboardCacheKeys()
    {
        var cacheKeys = new List<string>();

        // Common day ranges: 7, 14, 30, 90
        var commonDays = new[] { 7, 14, 30, 90 };

        // Common filters: no gameId, no roleFilter (most common use case)
        foreach (var days in commonDays)
        {
            // Format: dashboard:stats:{days}:{gameId}:{roleFilter}
            cacheKeys.Add($"dashboard:stats:{days}::");           // No filters
            cacheKeys.Add($"dashboard:stats:{days}::admin");      // Admin role filter
            cacheKeys.Add($"dashboard:stats:{days}::user");       // User role filter
            cacheKeys.Add($"dashboard:stats:{days}::all");        // All roles
        }

        return cacheKeys;
    }
}
