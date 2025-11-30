using Api.BoundedContexts.KnowledgeBase.Domain.Events;
using Api.Infrastructure;
using Api.SharedKernel.Application.EventHandlers;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.KnowledgeBase.Application.EventHandlers;

public sealed class MessageAddedEventHandler : DomainEventHandlerBase<MessageAddedEvent>
{
    public MessageAddedEventHandler(
        MeepleAiDbContext dbContext,
        ILogger<DomainEventHandlerBase<MessageAddedEvent>> logger)
        : base(dbContext, logger)
    {
    }

    protected override async Task HandleEventAsync(MessageAddedEvent domainEvent, CancellationToken cancellationToken)
    {
        // Auto-audit logging is handled by base class
        await Task.CompletedTask.ConfigureAwait(false);
    }

    protected override Dictionary<string, object?>? GetAuditMetadata(MessageAddedEvent domainEvent)
    {
        return new Dictionary<string, object?>
(StringComparer.Ordinal)
        {
            ["Action"] = "MessageAdded",
            ["ThreadId"] = domainEvent.ThreadId,
            ["MessageId"] = domainEvent.MessageId,
            ["Role"] = domainEvent.Role,
            ["ContentLength"] = domainEvent.ContentLength
        };
    }
}
