using Api.SharedKernel.Domain.Events;

namespace Api.BoundedContexts.KnowledgeBase.Domain.Events;

public sealed class ThreadReopenedEvent : DomainEventBase
{
    public Guid ThreadId { get; }

    public ThreadReopenedEvent(Guid threadId)
    {
        ThreadId = threadId;
    }
}
