using Api.BoundedContexts.KnowledgeBase.Domain.Events;
using Api.Infrastructure;
using Api.SharedKernel.Application.EventHandlers;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.KnowledgeBase.Application.EventHandlers;

public sealed class AgentActivatedEventHandler : DomainEventHandlerBase<AgentActivatedEvent>
{
    public AgentActivatedEventHandler(
        MeepleAiDbContext dbContext,
        ILogger<DomainEventHandlerBase<AgentActivatedEvent>> logger)
        : base(dbContext, logger)
    {
    }

    protected override async Task HandleEventAsync(AgentActivatedEvent domainEvent, CancellationToken cancellationToken)
    {
        // Auto-audit logging is handled by base class
        await Task.CompletedTask;
    }

    protected override Dictionary<string, object?>? GetAuditMetadata(AgentActivatedEvent domainEvent)
    {
        return new Dictionary<string, object?>
        {
            ["Action"] = "AgentActivated",
            ["AgentId"] = domainEvent.AgentId
        };
    }
}
