using Api.SharedKernel.Domain.Events;

namespace Api.BoundedContexts.KnowledgeBase.Domain.Events;

public sealed class MessageDeletedEvent : DomainEventBase
{
    public Guid ThreadId { get; }
    public Guid MessageId { get; }

    public MessageDeletedEvent(Guid threadId, Guid messageId)
    {
        ThreadId = threadId;
        MessageId = messageId;
    }
}
