using Api.BoundedContexts.KnowledgeBase.Domain.Events;
using Api.Infrastructure;
using Api.SharedKernel.Application.EventHandlers;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.KnowledgeBase.Application.EventHandlers;

public sealed class AgentCreatedEventHandler : DomainEventHandlerBase<AgentCreatedEvent>
{
    public AgentCreatedEventHandler(
        MeepleAiDbContext dbContext,
        ILogger<DomainEventHandlerBase<AgentCreatedEvent>> logger)
        : base(dbContext, logger)
    {
    }

    protected override async Task HandleEventAsync(AgentCreatedEvent domainEvent, CancellationToken cancellationToken)
    {
        // Auto-audit logging is handled by base class
        await Task.CompletedTask.ConfigureAwait(false);
    }

    protected override Dictionary<string, object?>? GetAuditMetadata(AgentCreatedEvent domainEvent)
    {
        return new Dictionary<string, object?>
        {
            ["Action"] = "AgentCreated",
            ["AgentId"] = domainEvent.AgentId,
            ["Type"] = domainEvent.Type,
            ["Name"] = domainEvent.Name
        };
    }
}
