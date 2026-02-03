using MediatR;

namespace Api.BoundedContexts.SessionTracking.Domain.Events;

/// <summary>
/// Domain event raised when a score is updated.
/// </summary>
public record ScoreUpdatedEvent : INotification
{
    /// <summary>
    /// Session unique identifier.
    /// </summary>
    public Guid SessionId { get; init; }

    /// <summary>
    /// Participant unique identifier.
    /// </summary>
    public Guid ParticipantId { get; init; }

    /// <summary>
    /// Score entry unique identifier.
    /// </summary>
    public Guid ScoreEntryId { get; init; }

    /// <summary>
    /// New score value.
    /// </summary>
    public decimal NewScore { get; init; }

    /// <summary>
    /// Optional round number.
    /// </summary>
    public int? RoundNumber { get; init; }

    /// <summary>
    /// Optional category.
    /// </summary>
    public string? Category { get; init; }

    /// <summary>
    /// When the score was updated.
    /// </summary>
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}