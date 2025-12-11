using Api.BoundedContexts.WorkflowIntegration.Domain.Events;
using Api.Infrastructure;
using Api.SharedKernel.Application.EventHandlers;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.WorkflowIntegration.Application.EventHandlers;

/// <summary>
/// Handler for N8NConfigurationUpdatedEvent domain event.
/// </summary>
public sealed class N8NConfigurationUpdatedEventHandler : DomainEventHandlerBase<N8NConfigurationUpdatedEvent>
{
    public N8NConfigurationUpdatedEventHandler(
        MeepleAiDbContext dbContext,
        ILogger<DomainEventHandlerBase<N8NConfigurationUpdatedEvent>> logger)
        : base(dbContext, logger)
    {
    }

    protected override async Task HandleEventAsync(N8NConfigurationUpdatedEvent domainEvent, CancellationToken cancellationToken)
    {
        // Future: Invalidate cached configuration
        // Future: Re-test connection if critical fields changed
        await Task.CompletedTask.ConfigureAwait(false);
    }

    protected override Dictionary<string, object?>? GetAuditMetadata(N8NConfigurationUpdatedEvent domainEvent)
    {
        return new Dictionary<string, object?>
(StringComparer.Ordinal)
        {
            ["ConfigurationId"] = domainEvent.ConfigurationId,
            ["Name"] = domainEvent.Name,
            ["BaseUrl"] = domainEvent.BaseUrl?.Value,
            ["WebhookUrl"] = domainEvent.WebhookUrl?.Value,
            ["IsActive"] = domainEvent.IsActive,
            ["Action"] = "N8NConfigurationUpdated"
        };
    }
}
