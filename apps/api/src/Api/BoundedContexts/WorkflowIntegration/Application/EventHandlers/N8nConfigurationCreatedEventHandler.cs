using Api.BoundedContexts.WorkflowIntegration.Domain.Events;
using Api.Infrastructure;
using Api.SharedKernel.Application.EventHandlers;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.WorkflowIntegration.Application.EventHandlers;

/// <summary>
/// Handler for N8nConfigurationCreatedEvent domain event.
/// </summary>
public sealed class N8nConfigurationCreatedEventHandler : DomainEventHandlerBase<N8nConfigurationCreatedEvent>
{
    public N8nConfigurationCreatedEventHandler(
        MeepleAiDbContext dbContext,
        ILogger<DomainEventHandlerBase<N8nConfigurationCreatedEvent>> logger)
        : base(dbContext, logger)
    {
    }

    protected override async Task HandleEventAsync(N8nConfigurationCreatedEvent domainEvent, CancellationToken cancellationToken)
    {
        // Future: Trigger configuration validation workflow
        // Future: Send notification to administrators
        await Task.CompletedTask.ConfigureAwait(false);
    }

    protected override Dictionary<string, object?>? GetAuditMetadata(N8nConfigurationCreatedEvent domainEvent)
    {
        return new Dictionary<string, object?>
        {
            ["ConfigurationId"] = domainEvent.ConfigurationId,
            ["Name"] = domainEvent.Name,
            ["BaseUrl"] = domainEvent.BaseUrl.Value,
            ["WebhookUrl"] = domainEvent.WebhookUrl?.Value,
            ["IsActive"] = domainEvent.IsActive,
            ["CreatedByUserId"] = domainEvent.CreatedByUserId,
            ["Action"] = "N8nConfigurationCreated"
        };
    }

    protected override Guid? GetUserId(N8nConfigurationCreatedEvent domainEvent)
    {
        return domainEvent.CreatedByUserId;
    }
}
