using MediatR;

namespace Api.BoundedContexts.SessionTracking.Domain.Events;

/// <summary>
/// Domain event raised when a session is finalized.
/// </summary>
public record SessionFinalizedEvent : INotification
{
    /// <summary>
    /// Session unique identifier.
    /// </summary>
    public Guid SessionId { get; init; }

    /// <summary>
    /// Winner participant ID (if applicable).
    /// </summary>
    public Guid? WinnerId { get; init; }

    /// <summary>
    /// Final ranks for all participants (ParticipantId -> Rank).
    /// </summary>
    public IReadOnlyDictionary<Guid, int> FinalRanks { get; init; } = new Dictionary<Guid, int>();

    /// <summary>
    /// Session duration in minutes.
    /// </summary>
    public int DurationMinutes { get; init; }

    /// <summary>
    /// When the session was finalized.
    /// </summary>
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}