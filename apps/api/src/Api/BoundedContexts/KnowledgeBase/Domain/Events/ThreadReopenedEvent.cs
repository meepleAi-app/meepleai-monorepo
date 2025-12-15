using Api.SharedKernel.Domain.Events;

namespace Api.BoundedContexts.KnowledgeBase.Domain.Events;

internal sealed class ThreadReopenedEvent : DomainEventBase
{
    public Guid ThreadId { get; }

    public ThreadReopenedEvent(Guid threadId)
    {
        ThreadId = threadId;
    }
}
