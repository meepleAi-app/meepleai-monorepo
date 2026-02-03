using Api.SharedKernel.Domain.Events;

namespace Api.BoundedContexts.KnowledgeBase.Domain.Events;

/// <summary>
/// Domain event raised when a message is added to a chat session.
/// Issue #3483: Chat Session Persistence Service.
/// </summary>
internal sealed class ChatSessionMessageAddedEvent : DomainEventBase
{
    public Guid SessionId { get; }
    public Guid MessageId { get; }
    public string Role { get; }

    public ChatSessionMessageAddedEvent(Guid sessionId, Guid messageId, string role)
    {
        SessionId = sessionId;
        MessageId = messageId;
        Role = role;
    }
}
