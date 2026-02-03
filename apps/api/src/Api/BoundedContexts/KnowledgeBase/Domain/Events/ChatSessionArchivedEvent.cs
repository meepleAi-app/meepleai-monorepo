using Api.SharedKernel.Domain.Events;

namespace Api.BoundedContexts.KnowledgeBase.Domain.Events;

/// <summary>
/// Domain event raised when a chat session is archived.
/// Issue #3483: Chat Session Persistence Service.
/// </summary>
internal sealed class ChatSessionArchivedEvent : DomainEventBase
{
    public Guid SessionId { get; }

    public ChatSessionArchivedEvent(Guid sessionId)
    {
        SessionId = sessionId;
    }
}
