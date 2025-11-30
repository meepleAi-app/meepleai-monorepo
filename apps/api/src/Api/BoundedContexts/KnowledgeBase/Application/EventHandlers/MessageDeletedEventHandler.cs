using Api.BoundedContexts.KnowledgeBase.Domain.Events;
using Api.Infrastructure;
using Api.SharedKernel.Application.EventHandlers;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.KnowledgeBase.Application.EventHandlers;

public sealed class MessageDeletedEventHandler : DomainEventHandlerBase<MessageDeletedEvent>
{
    public MessageDeletedEventHandler(
        MeepleAiDbContext dbContext,
        ILogger<DomainEventHandlerBase<MessageDeletedEvent>> logger)
        : base(dbContext, logger)
    {
    }

    protected override async Task HandleEventAsync(MessageDeletedEvent domainEvent, CancellationToken cancellationToken)
    {
        // Auto-audit logging is handled by base class
        await Task.CompletedTask.ConfigureAwait(false);
    }

    protected override Dictionary<string, object?>? GetAuditMetadata(MessageDeletedEvent domainEvent)
    {
        return new Dictionary<string, object?>
(StringComparer.Ordinal)
        {
            ["Action"] = "MessageDeleted",
            ["ThreadId"] = domainEvent.ThreadId,
            ["MessageId"] = domainEvent.MessageId
        };
    }
}
