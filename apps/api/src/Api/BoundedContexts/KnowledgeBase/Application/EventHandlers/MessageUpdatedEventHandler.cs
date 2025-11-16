using Api.BoundedContexts.KnowledgeBase.Domain.Events;
using Api.Infrastructure;
using Api.SharedKernel.Application.EventHandlers;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.KnowledgeBase.Application.EventHandlers;

public sealed class MessageUpdatedEventHandler : DomainEventHandlerBase<MessageUpdatedEvent>
{
    public MessageUpdatedEventHandler(
        MeepleAiDbContext dbContext,
        ILogger<DomainEventHandlerBase<MessageUpdatedEvent>> logger)
        : base(dbContext, logger)
    {
    }

    protected override async Task HandleEventAsync(MessageUpdatedEvent domainEvent, CancellationToken cancellationToken)
    {
        // Auto-audit logging is handled by base class
        await Task.CompletedTask;
    }

    protected override Dictionary<string, object?>? GetAuditMetadata(MessageUpdatedEvent domainEvent)
    {
        return new Dictionary<string, object?>
        {
            ["Action"] = "MessageUpdated",
            ["ThreadId"] = domainEvent.ThreadId,
            ["MessageId"] = domainEvent.MessageId,
            ["NewContentLength"] = domainEvent.NewContentLength
        };
    }
}
