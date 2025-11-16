using Api.SharedKernel.Domain.Events;

namespace Api.BoundedContexts.KnowledgeBase.Domain.Events;

public sealed class MessageAddedEvent : DomainEventBase
{
    public Guid ThreadId { get; }
    public Guid MessageId { get; }
    public string Role { get; }
    public int ContentLength { get; }

    public MessageAddedEvent(Guid threadId, Guid messageId, string role, int contentLength)
    {
        ThreadId = threadId;
        MessageId = messageId;
        Role = role;
        ContentLength = contentLength;
    }
}
