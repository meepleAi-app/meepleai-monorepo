using Api.SharedKernel.Domain.Events;

namespace Api.BoundedContexts.KnowledgeBase.Domain.Events;

/// <summary>
/// Domain event raised when a chat session is created.
/// Issue #3483: Chat Session Persistence Service.
/// </summary>
internal sealed class ChatSessionCreatedEvent : DomainEventBase
{
    public Guid SessionId { get; }
    public Guid UserId { get; }
    public Guid GameId { get; }

    public ChatSessionCreatedEvent(Guid sessionId, Guid userId, Guid gameId)
    {
        SessionId = sessionId;
        UserId = userId;
        GameId = gameId;
    }
}
