using Api.BoundedContexts.SystemConfiguration.Domain.Events;
using Api.Infrastructure;
using Api.Services;
using Api.SharedKernel.Application.EventHandlers;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.SystemConfiguration.Application.EventHandlers;

/// <summary>
/// Handler for ConfigurationToggledEvent domain event.
/// Invalidates the cache of the toggled key.
///
/// <para>
/// The audit row is NOT written here: since #1534 it comes from the open-generic
/// <see cref="Api.SharedKernel.Application.EventHandlers.DomainEventAuditHandler{TEvent}"/>, and the
/// <c>GetUserId</c>/<c>GetAuditMetadata</c> overrides below are dead hooks of the base class.
/// </para>
/// </summary>
internal sealed class ConfigurationToggledEventHandler : DomainEventHandlerBase<ConfigurationToggledEvent>
{
    private readonly IHybridCacheService _cache;

    public ConfigurationToggledEventHandler(
        MeepleAiDbContext dbContext,
        IHybridCacheService cache,
        ILogger<ConfigurationToggledEventHandler> logger)
        : base(dbContext, logger)
    {
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
    }

    protected override async Task HandleEventAsync(ConfigurationToggledEvent domainEvent, CancellationToken cancellationToken)
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

        // Future: Send notification for feature flag toggles
        // Future: Track feature flag usage metrics
    }

    protected override Dictionary<string, object?>? GetAuditMetadata(ConfigurationToggledEvent domainEvent)
    {
        return new Dictionary<string, object?>
(StringComparer.Ordinal)
        {
            ["ConfigurationId"] = domainEvent.ConfigurationId,
            ["Key"] = domainEvent.Key.Value,
            ["IsActive"] = domainEvent.IsActive,
            ["ToggledByUserId"] = domainEvent.ToggledByUserId,
            ["Action"] = "ConfigurationToggled",
            ["Status"] = domainEvent.IsActive ? "Activated" : "Deactivated"
        };
    }

    protected override Guid? GetUserId(ConfigurationToggledEvent domainEvent)
    {
        return domainEvent.ToggledByUserId;
    }
}
