using Api.BoundedContexts.SystemConfiguration.Domain.Events;
using Api.Infrastructure;
using Api.Services;
using Api.SharedKernel.Application.EventHandlers;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.SystemConfiguration.Application.EventHandlers;

/// <summary>
/// Handler for ConfigurationDeletedEvent domain event.
/// Automatically creates audit log entry and invalidates cache via base class.
/// </summary>
public sealed class ConfigurationDeletedEventHandler : DomainEventHandlerBase<ConfigurationDeletedEvent>
{
    private readonly IHybridCacheService _cache;

    public ConfigurationDeletedEventHandler(
        MeepleAiDbContext dbContext,
        IHybridCacheService cache,
        ILogger<DomainEventHandlerBase<ConfigurationDeletedEvent>> logger)
        : base(dbContext, logger)
    {
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
    }

    protected override async Task HandleEventAsync(ConfigurationDeletedEvent domainEvent, CancellationToken cancellationToken)
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

        // Future: Send notification to administrators for configuration deletion
        // Future: Archive configuration value before deletion (soft delete pattern)
    }

    protected override Dictionary<string, object?>? GetAuditMetadata(ConfigurationDeletedEvent domainEvent)
    {
        return new Dictionary<string, object?>
(StringComparer.Ordinal)
        {
            ["ConfigurationId"] = domainEvent.ConfigurationId,
            ["Key"] = domainEvent.Key.Value,
            ["Category"] = domainEvent.Category,
            ["Environment"] = domainEvent.Environment,
            ["Action"] = "ConfigurationDeleted"
        };
    }

    protected override Guid? GetUserId(ConfigurationDeletedEvent domainEvent)
    {
        // Deletion event doesn't have explicit user ID
        // The base class will handle this gracefully
        return null;
    }
}
