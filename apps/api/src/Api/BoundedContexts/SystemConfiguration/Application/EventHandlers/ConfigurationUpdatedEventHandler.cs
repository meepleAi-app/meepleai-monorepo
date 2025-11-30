using Api.BoundedContexts.SystemConfiguration.Domain.Events;
using Api.Infrastructure;
using Api.Services;
using Api.SharedKernel.Application.EventHandlers;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.SystemConfiguration.Application.EventHandlers;

/// <summary>
/// Handler for ConfigurationUpdatedEvent domain event.
/// Automatically creates audit log entry and invalidates cache via base class.
/// </summary>
public sealed class ConfigurationUpdatedEventHandler : DomainEventHandlerBase<ConfigurationUpdatedEvent>
{
    private readonly IHybridCacheService _cache;

    public ConfigurationUpdatedEventHandler(
        MeepleAiDbContext dbContext,
        IHybridCacheService cache,
        ILogger<DomainEventHandlerBase<ConfigurationUpdatedEvent>> logger)
        : base(dbContext, logger)
    {
        _cache = cache;
    }

    protected override async Task HandleEventAsync(ConfigurationUpdatedEvent domainEvent, CancellationToken cancellationToken)
    {
        // Invalidate cache across all environments (fixes cache key mismatch bug)
        var environments = new[] { "Development", "Staging", "Production", "All" };
        foreach (var env in environments)
        {
            var cacheKey = $"config:{domainEvent.Key.Value}:{env}";
            await _cache.RemoveAsync(cacheKey, cancellationToken).ConfigureAwait(false);
        }

        // Also use tag-based invalidation for broader cleanup
        await _cache.RemoveByTagAsync("config:category:general", cancellationToken).ConfigureAwait(false);

        // Future: Send notification for critical configuration changes (e.g., rate limits, feature flags)
        // Future: Check if RequiresRestart and alert administrators
    }

    protected override Dictionary<string, object?>? GetAuditMetadata(ConfigurationUpdatedEvent domainEvent)
    {
        return new Dictionary<string, object?>
(StringComparer.Ordinal)
        {
            ["ConfigurationId"] = domainEvent.ConfigurationId,
            ["Key"] = domainEvent.Key.Value,
            ["PreviousValue"] = domainEvent.PreviousValue,
            ["NewValue"] = domainEvent.NewValue,
            ["Version"] = domainEvent.Version,
            ["UpdatedByUserId"] = domainEvent.UpdatedByUserId,
            ["Action"] = "ConfigurationUpdated"
        };
    }

    protected override Guid? GetUserId(ConfigurationUpdatedEvent domainEvent)
    {
        return domainEvent.UpdatedByUserId;
    }
}
