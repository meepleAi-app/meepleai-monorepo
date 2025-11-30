using Api.BoundedContexts.KnowledgeBase.Domain.Events;
using Api.Infrastructure;
using Api.SharedKernel.Application.EventHandlers;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.KnowledgeBase.Application.EventHandlers;

public sealed class ThreadClosedEventHandler : DomainEventHandlerBase<ThreadClosedEvent>
{
    public ThreadClosedEventHandler(
        MeepleAiDbContext dbContext,
        ILogger<DomainEventHandlerBase<ThreadClosedEvent>> logger)
        : base(dbContext, logger)
    {
    }

    protected override async Task HandleEventAsync(ThreadClosedEvent domainEvent, CancellationToken cancellationToken)
    {
        // Auto-audit logging is handled by base class
        await Task.CompletedTask.ConfigureAwait(false);
    }

    protected override Dictionary<string, object?>? GetAuditMetadata(ThreadClosedEvent domainEvent)
    {
        return new Dictionary<string, object?>
        {
            ["Action"] = "ThreadClosed",
            ["ThreadId"] = domainEvent.ThreadId,
            ["TotalMessages"] = domainEvent.TotalMessages
        };
    }
}
