using Api.BoundedContexts.SystemConfiguration.Domain.Events;
using Api.Infrastructure;
using Api.Services;
using Api.SharedKernel.Application.EventHandlers;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.SystemConfiguration.Application.EventHandlers;

/// <summary>
/// Handler for ConfigurationCreatedEvent domain event.
/// Drops the cached lookups of the new key (see <c>HandleEventAsync</c>); the rest is the base class logging.
///
/// <para>
/// The audit row is NOT written here: since #1534 it comes from the open-generic
/// <see cref="Api.SharedKernel.Application.EventHandlers.DomainEventAuditHandler{TEvent}"/>, and the
/// <c>GetUserId</c>/<c>GetAuditMetadata</c> overrides below are dead hooks of the base class.
/// </para>
/// </summary>
internal sealed class ConfigurationCreatedEventHandler : DomainEventHandlerBase<ConfigurationCreatedEvent>
{
    private readonly IHybridCacheService _cache;

    public ConfigurationCreatedEventHandler(
        MeepleAiDbContext dbContext,
        IHybridCacheService cache,
        ILogger<ConfigurationCreatedEventHandler> logger)
        : base(dbContext, logger)
    {
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
    }

    protected override async Task HandleEventAsync(ConfigurationCreatedEvent domainEvent, CancellationToken cancellationToken)
    {
        // The audit row comes from DomainEventAuditHandler, not from here (see the class summary)
        // Future: Send notification to administrators for critical configurations
        // Future: Trigger configuration validation workflow

        // #4698: the reader caches a lookup that found nothing, so a key created after such a lookup stays invisible
        // until the entry expires. Same entries the Updated/Toggled/Deleted handlers drop, plus the event's own
        // environment (a custom one is not in the fixed list).
        var environments = new[] { "Development", "Staging", "Production", "All", domainEvent.Environment };
        foreach (var env in environments.Distinct(StringComparer.Ordinal))
        {
            await _cache.RemoveAsync($"config:{domainEvent.Key.Value}:{env}", cancellationToken).ConfigureAwait(false);
        }

        await _cache.RemoveByTagAsync("config:category:general", cancellationToken).ConfigureAwait(false);
    }

    protected override Dictionary<string, object?>? GetAuditMetadata(ConfigurationCreatedEvent domainEvent)
    {
        return new Dictionary<string, object?>
(StringComparer.Ordinal)
        {
            ["ConfigurationId"] = domainEvent.ConfigurationId,
            ["Key"] = domainEvent.Key.Value,
            ["Value"] = domainEvent.Value,
            ["ValueType"] = domainEvent.ValueType,
            ["Category"] = domainEvent.Category,
            ["Environment"] = domainEvent.Environment,
            ["RequiresRestart"] = domainEvent.RequiresRestart,
            ["CreatedByUserId"] = domainEvent.CreatedByUserId,
            ["Action"] = "ConfigurationCreated"
        };
    }

    protected override Guid? GetUserId(ConfigurationCreatedEvent domainEvent)
    {
        return domainEvent.CreatedByUserId;
    }
}
