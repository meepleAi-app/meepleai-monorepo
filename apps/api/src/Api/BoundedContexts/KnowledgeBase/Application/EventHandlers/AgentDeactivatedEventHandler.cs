using Api.BoundedContexts.KnowledgeBase.Domain.Events;
using Api.Infrastructure;
using Api.SharedKernel.Application.EventHandlers;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.KnowledgeBase.Application.EventHandlers;

public sealed class AgentDeactivatedEventHandler : DomainEventHandlerBase<AgentDeactivatedEvent>
{
    public AgentDeactivatedEventHandler(
        MeepleAiDbContext dbContext,
        ILogger<DomainEventHandlerBase<AgentDeactivatedEvent>> logger)
        : base(dbContext, logger)
    {
    }

    protected override async Task HandleEventAsync(AgentDeactivatedEvent domainEvent, CancellationToken cancellationToken)
    {
        // Auto-audit logging is handled by base class
        await Task.CompletedTask;
    }

    protected override Dictionary<string, object?>? GetAuditMetadata(AgentDeactivatedEvent domainEvent)
    {
        return new Dictionary<string, object?>
        {
            ["Action"] = "AgentDeactivated",
            ["AgentId"] = domainEvent.AgentId
        };
    }
}
