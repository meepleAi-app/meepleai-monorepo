using Api.SharedKernel.Domain.Events;

namespace Api.BoundedContexts.KnowledgeBase.Domain.Events;

internal sealed class MessageUpdatedEvent : DomainEventBase
{
    public Guid ThreadId { get; }
    public Guid MessageId { get; }
    public int NewContentLength { get; }

    public MessageUpdatedEvent(Guid threadId, Guid messageId, int newContentLength)
    {
        ThreadId = threadId;
        MessageId = messageId;
        NewContentLength = newContentLength;
    }
}
