using Api.BoundedContexts.SystemConfiguration.Domain.Events;
using Api.Infrastructure;
using Api.SharedKernel.Application.EventHandlers;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.SystemConfiguration.Application.EventHandlers;

/// <summary>
/// Handler for ConfigurationCreatedEvent domain event.
/// Automatically creates audit log entry via base class.
/// </summary>
public sealed class ConfigurationCreatedEventHandler : DomainEventHandlerBase<ConfigurationCreatedEvent>
{
    public ConfigurationCreatedEventHandler(
        MeepleAiDbContext dbContext,
        ILogger<DomainEventHandlerBase<ConfigurationCreatedEvent>> logger)
        : base(dbContext, logger)
    {
    }

    protected override async Task HandleEventAsync(ConfigurationCreatedEvent domainEvent, CancellationToken cancellationToken)
    {
        // Base class automatically creates audit log entry
        // Future: Send notification to administrators for critical configurations
        // Future: Trigger configuration validation workflow
        await Task.CompletedTask.ConfigureAwait(false);
    }

    protected override Dictionary<string, object?>? GetAuditMetadata(ConfigurationCreatedEvent domainEvent)
    {
        return new Dictionary<string, object?>
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
