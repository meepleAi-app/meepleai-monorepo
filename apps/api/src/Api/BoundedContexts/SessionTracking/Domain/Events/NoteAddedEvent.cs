using Api.BoundedContexts.SessionTracking.Domain.Entities;
using MediatR;

namespace Api.BoundedContexts.SessionTracking.Domain.Events;

/// <summary>
/// Domain event raised when a note is added to the session.
/// For SSE: only emitted for NoteType.Shared notes (Private notes are not broadcasted).
/// </summary>
public record NoteAddedEvent : INotification
{
    /// <summary>
    /// Session unique identifier.
    /// </summary>
    public required Guid SessionId { get; init; }

    /// <summary>
    /// ID of the note that was added.
    /// </summary>
    public required Guid NoteId { get; init; }

    /// <summary>
    /// Participant who added the note.
    /// </summary>
    public required Guid ParticipantId { get; init; }

    /// <summary>
    /// Type of note (Private, Shared, Template).
    /// </summary>
    public required NoteType NoteType { get; init; }

    /// <summary>
    /// Note content.
    /// </summary>
    public required string Content { get; init; }

    /// <summary>
    /// Whether the note is hidden from other participants.
    /// </summary>
    public required bool IsHidden { get; init; }

    /// <summary>
    /// When the note was added.
    /// </summary>
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}
