using Api.BoundedContexts.KnowledgeBase.Domain.Events;
using Api.Infrastructure;
using Api.SharedKernel.Application.EventHandlers;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.KnowledgeBase.Application.EventHandlers;

internal sealed class ThreadReopenedEventHandler : DomainEventHandlerBase<ThreadReopenedEvent>
{
    public ThreadReopenedEventHandler(
        MeepleAiDbContext dbContext,
        ILogger<DomainEventHandlerBase<ThreadReopenedEvent>> logger)
        : base(dbContext, logger)
    {
    }

    protected override async Task HandleEventAsync(ThreadReopenedEvent domainEvent, CancellationToken cancellationToken)
    {
        // Auto-audit logging is handled by base class
        await Task.CompletedTask.ConfigureAwait(false);
    }

    protected override Dictionary<string, object?>? GetAuditMetadata(ThreadReopenedEvent domainEvent)
    {
        return new Dictionary<string, object?>
(StringComparer.Ordinal)
        {
            ["Action"] = "ThreadReopened",
            ["ThreadId"] = domainEvent.ThreadId
        };
    }
}
