using MediatR;

namespace Api.BoundedContexts.SessionTracking.Domain.Events;

/// <summary>
/// Domain event raised when a new participant joins the session.
/// Also used for SSE broadcasting to connected clients.
/// </summary>
public record ParticipantAddedEvent : INotification
{
    /// <summary>
    /// Session unique identifier.
    /// </summary>
    public required Guid SessionId { get; init; }

    /// <summary>
    /// ID of the participant that was added.
    /// </summary>
    public required Guid ParticipantId { get; init; }

    /// <summary>
    /// Display name of the participant.
    /// </summary>
    public required string DisplayName { get; init; }

    /// <summary>
    /// Whether the participant is the session owner.
    /// </summary>
    public required bool IsOwner { get; init; }

    /// <summary>
    /// Join order (1-based).
    /// </summary>
    public required int JoinOrder { get; init; }

    /// <summary>
    /// When the participant was added.
    /// </summary>
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}
