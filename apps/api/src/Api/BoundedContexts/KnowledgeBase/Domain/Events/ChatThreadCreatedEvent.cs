using Api.SharedKernel.Domain.Events;

namespace Api.BoundedContexts.KnowledgeBase.Domain.Events;

internal sealed class ChatThreadCreatedEvent : DomainEventBase
{
    public Guid ThreadId { get; }
    public Guid GameId { get; }
    public Guid UserId { get; }

    public ChatThreadCreatedEvent(Guid threadId, Guid gameId, Guid userId)
    {
        ThreadId = threadId;
        GameId = gameId;
        UserId = userId;
    }
}
