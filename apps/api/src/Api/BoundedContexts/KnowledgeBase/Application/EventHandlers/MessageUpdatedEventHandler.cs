using Api.BoundedContexts.KnowledgeBase.Domain.Events;
using Api.Infrastructure;
using Api.SharedKernel.Application.EventHandlers;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.KnowledgeBase.Application.EventHandlers;

internal sealed class MessageUpdatedEventHandler : DomainEventHandlerBase<MessageUpdatedEvent>
{
    public MessageUpdatedEventHandler(
        MeepleAiDbContext dbContext,
        ILogger<MessageUpdatedEventHandler> logger)
        : base(dbContext, logger)
    {
    }

    protected override async Task HandleEventAsync(MessageUpdatedEvent domainEvent, CancellationToken cancellationToken)
    {
        // Auto-audit logging is handled by base class
        await Task.CompletedTask.ConfigureAwait(false);
    }

    protected override Dictionary<string, object?>? GetAuditMetadata(MessageUpdatedEvent domainEvent)
    {
        return new Dictionary<string, object?>
(StringComparer.Ordinal)
        {
            ["Action"] = "MessageUpdated",
            ["ThreadId"] = domainEvent.ThreadId,
            ["MessageId"] = domainEvent.MessageId,
            ["NewContentLength"] = domainEvent.NewContentLength
        };
    }
}
