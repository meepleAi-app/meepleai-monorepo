using Api.BoundedContexts.WorkflowIntegration.Domain.Events;
using Api.Infrastructure;
using Api.SharedKernel.Application.EventHandlers;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.WorkflowIntegration.Application.EventHandlers;

/// <summary>
/// Handler for N8NConfigurationCreatedEvent domain event.
/// </summary>
public sealed class N8NConfigurationCreatedEventHandler : DomainEventHandlerBase<N8NConfigurationCreatedEvent>
{
    public N8NConfigurationCreatedEventHandler(
        MeepleAiDbContext dbContext,
        ILogger<DomainEventHandlerBase<N8NConfigurationCreatedEvent>> logger)
        : base(dbContext, logger)
    {
    }

    protected override async Task HandleEventAsync(N8NConfigurationCreatedEvent domainEvent, CancellationToken cancellationToken)
    {
        // Future: Trigger configuration validation workflow
        // Future: Send notification to administrators
        await Task.CompletedTask.ConfigureAwait(false);
    }

    protected override Dictionary<string, object?>? GetAuditMetadata(N8NConfigurationCreatedEvent domainEvent)
    {
        return new Dictionary<string, object?>
(StringComparer.Ordinal)
        {
            ["ConfigurationId"] = domainEvent.ConfigurationId,
            ["Name"] = domainEvent.Name,
            ["BaseUrl"] = domainEvent.BaseUrl.Value,
            ["WebhookUrl"] = domainEvent.WebhookUrl?.Value,
            ["IsActive"] = domainEvent.IsActive,
            ["CreatedByUserId"] = domainEvent.CreatedByUserId,
            ["Action"] = "N8NConfigurationCreated"
        };
    }

    protected override Guid? GetUserId(N8NConfigurationCreatedEvent domainEvent)
    {
        return domainEvent.CreatedByUserId;
    }
}
