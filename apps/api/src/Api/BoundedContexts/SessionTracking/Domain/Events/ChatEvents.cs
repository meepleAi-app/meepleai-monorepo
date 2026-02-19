using Api.BoundedContexts.SessionTracking.Domain.Entities;
using MediatR;

namespace Api.BoundedContexts.SessionTracking.Domain.Events;

/// <summary>
/// Domain event raised when a chat message is sent in a session.
/// Issue #4760 - SessionMedia Entity + RAG Agent Integration + Shared Chat
/// </summary>
public record SessionChatMessageSentEvent : INotification
{
    public required Guid SessionId { get; init; }
    public required Guid MessageId { get; init; }
    public Guid? SenderId { get; init; }
    public required SessionChatMessageType MessageType { get; init; }
    public required string Content { get; init; }
    public int? TurnNumber { get; init; }
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}
