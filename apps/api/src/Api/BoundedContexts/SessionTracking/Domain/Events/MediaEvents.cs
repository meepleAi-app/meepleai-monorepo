using Api.BoundedContexts.SessionTracking.Domain.Entities;
using MediatR;

namespace Api.BoundedContexts.SessionTracking.Domain.Events;

/// <summary>
/// Domain event raised when media is uploaded to a session.
/// Issue #4760 - SessionMedia Entity + RAG Agent Integration + Shared Chat
/// </summary>
public record SessionMediaUploadedEvent : INotification
{
    public required Guid SessionId { get; init; }
    public required Guid MediaId { get; init; }
    public required Guid ParticipantId { get; init; }
    public required SessionMediaType MediaType { get; init; }
    public required string FileName { get; init; }
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// Domain event raised when media is deleted from a session.
/// </summary>
public record SessionMediaDeletedEvent : INotification
{
    public required Guid SessionId { get; init; }
    public required Guid MediaId { get; init; }
    public required Guid ParticipantId { get; init; }
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}
