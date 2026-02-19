using MediatR;

namespace Api.BoundedContexts.SessionTracking.Domain.Events;

/// <summary>
/// Raised when a participant is kicked from a session by the host.
/// Issue #4765 - Player Action Endpoints + Host Validation
/// </summary>
public record ParticipantKickedEvent : INotification
{
    public required Guid SessionId { get; init; }
    public required Guid ParticipantId { get; init; }
    public required string DisplayName { get; init; }
    public required Guid KickedBy { get; init; }
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}
