using Api.BoundedContexts.KnowledgeBase.Domain.Events;
using Api.Infrastructure;
using Api.SharedKernel.Application.EventHandlers;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.KnowledgeBase.Application.EventHandlers;

internal sealed class AgentInvokedEventHandler : DomainEventHandlerBase<AgentInvokedEvent>
{
    public AgentInvokedEventHandler(
        MeepleAiDbContext dbContext,
        ILogger<DomainEventHandlerBase<AgentInvokedEvent>> logger)
        : base(dbContext, logger)
    {
    }

    protected override async Task HandleEventAsync(AgentInvokedEvent domainEvent, CancellationToken cancellationToken)
    {
        // Auto-audit logging is handled by base class
        await Task.CompletedTask.ConfigureAwait(false);
    }

    protected override Dictionary<string, object?>? GetAuditMetadata(AgentInvokedEvent domainEvent)
    {
        return new Dictionary<string, object?>
(StringComparer.Ordinal)
        {
            ["Action"] = "AgentInvoked",
            ["AgentId"] = domainEvent.AgentId,
            ["Input"] = domainEvent.Input,
            ["TokensUsed"] = domainEvent.TokensUsed
        };
    }
}
