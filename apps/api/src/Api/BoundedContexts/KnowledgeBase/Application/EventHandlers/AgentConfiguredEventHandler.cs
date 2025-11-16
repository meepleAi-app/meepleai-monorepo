using Api.BoundedContexts.KnowledgeBase.Domain.Events;
using Api.Infrastructure;
using Api.SharedKernel.Application.EventHandlers;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.KnowledgeBase.Application.EventHandlers;

public sealed class AgentConfiguredEventHandler : DomainEventHandlerBase<AgentConfiguredEvent>
{
    public AgentConfiguredEventHandler(
        MeepleAiDbContext dbContext,
        ILogger<DomainEventHandlerBase<AgentConfiguredEvent>> logger)
        : base(dbContext, logger)
    {
    }

    protected override async Task HandleEventAsync(AgentConfiguredEvent domainEvent, CancellationToken cancellationToken)
    {
        // Auto-audit logging is handled by base class
        await Task.CompletedTask;
    }

    protected override Dictionary<string, object?>? GetAuditMetadata(AgentConfiguredEvent domainEvent)
    {
        return new Dictionary<string, object?>
        {
            ["Action"] = "AgentConfigured",
            ["AgentId"] = domainEvent.AgentId,
            ["ConfigurationJson"] = domainEvent.ConfigurationJson
        };
    }
}
