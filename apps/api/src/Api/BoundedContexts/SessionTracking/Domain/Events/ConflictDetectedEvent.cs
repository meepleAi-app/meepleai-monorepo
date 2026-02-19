using MediatR;

namespace Api.BoundedContexts.SessionTracking.Domain.Events;

/// <summary>
/// Raised when an optimistic concurrency conflict is detected on a session action.
/// Delivered privately to the affected user via SSE.
/// Issue #4765 - Conflict Resolution
/// </summary>
public record ConflictDetectedEvent : INotification
{
    public required Guid SessionId { get; init; }
    public required Guid AffectedUserId { get; init; }
    public required string ActionType { get; init; }
    public required string Message { get; init; }
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}
