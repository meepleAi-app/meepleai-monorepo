using Api.SharedKernel.Domain.Events;

namespace Api.BoundedContexts.KnowledgeBase.Domain.Events;

internal sealed class ThreadClosedEvent : DomainEventBase
{
    public Guid ThreadId { get; }
    public int TotalMessages { get; }

    public ThreadClosedEvent(Guid threadId, int totalMessages)
    {
        ThreadId = threadId;
        TotalMessages = totalMessages;
    }
}
