using Api.BoundedContexts.KnowledgeBase.Domain.Events;
using Api.Infrastructure;
using Api.SharedKernel.Application.EventHandlers;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.KnowledgeBase.Application.EventHandlers;

public sealed class ChatThreadCreatedEventHandler : DomainEventHandlerBase<ChatThreadCreatedEvent>
{
    public ChatThreadCreatedEventHandler(
        MeepleAiDbContext dbContext,
        ILogger<DomainEventHandlerBase<ChatThreadCreatedEvent>> logger)
        : base(dbContext, logger)
    {
    }

    protected override async Task HandleEventAsync(ChatThreadCreatedEvent domainEvent, CancellationToken cancellationToken)
    {
        // Auto-audit logging is handled by base class
        await Task.CompletedTask;
    }

    protected override Dictionary<string, object?>? GetAuditMetadata(ChatThreadCreatedEvent domainEvent)
    {
        return new Dictionary<string, object?>
        {
            ["Action"] = "ChatThreadCreated",
            ["ThreadId"] = domainEvent.ThreadId,
            ["GameId"] = domainEvent.GameId,
            ["UserId"] = domainEvent.UserId
        };
    }

    protected override Guid? GetUserId(ChatThreadCreatedEvent domainEvent)
    {
        return domainEvent.UserId;
    }
}
