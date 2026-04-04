using System.ComponentModel.DataAnnotations;

namespace Api.BoundedContexts.SessionTracking.Domain.Entities;

/// <summary>
/// Domain entity representing a session timeline event (Session Diary).
/// Tracks all notable actions in a game session for timeline reconstruction.
/// Issue #276 - Session Diary / Timeline
/// </summary>
public class SessionEvent
{
    /// <summary>
    /// Unique identifier for the session event.
    /// </summary>
    public Guid Id { get; private set; }

    /// <summary>
    /// Session this event belongs to.
    /// </summary>
    public Guid SessionId { get; private set; }

    /// <summary>
    /// Type of event (e.g., "dice_roll", "score_update", "player_joined", "note_added").
    /// </summary>
    [MaxLength(50)]
    public string EventType { get; private set; } = string.Empty;

    /// <summary>
    /// When the event occurred.
    /// </summary>
    public DateTime Timestamp { get; private set; } = DateTime.UtcNow;

    /// <summary>
    /// JSON payload with event-specific data.
    /// </summary>
    public string Payload { get; private set; } = "{}";

    /// <summary>
    /// User who triggered the event (null for system events).
    /// </summary>
    public Guid? CreatedBy { get; private set; }

    /// <summary>
    /// Source of the event (e.g., "user", "system", "integration").
    /// </summary>
    [MaxLength(50)]
    public string? Source { get; private set; }

    /// <summary>
    /// Soft delete flag.
    /// </summary>
    public bool IsDeleted { get; private set; }

    /// <summary>
    /// Soft delete timestamp.
    /// </summary>
    public DateTime? DeletedAt { get; private set; }

    /// <summary>
    /// Private constructor for EF Core.
    /// </summary>
    private SessionEvent()
    {
    }

    /// <summary>
    /// Factory method to create a new session event.
    /// </summary>
    /// <param name="sessionId">Session where the event occurs.</param>
    /// <param name="eventType">Type of event (max 50 chars).</param>
    /// <param name="payload">JSON payload with event data (defaults to "{}").</param>
    /// <param name="createdBy">User who triggered the event (null for system events).</param>
    /// <param name="source">Source of the event (max 50 chars).</param>
    /// <returns>New SessionEvent instance.</returns>
    public static SessionEvent Create(
        Guid sessionId,
        string eventType,
        string? payload = null,
        Guid? createdBy = null,
        string? source = null)
    {
        if (sessionId == Guid.Empty)
            throw new ArgumentException("Session ID cannot be empty.", nameof(sessionId));

        if (string.IsNullOrWhiteSpace(eventType))
            throw new ArgumentException("Event type cannot be empty.", nameof(eventType));

        if (eventType.Length > 50)
            throw new ArgumentException("Event type must be 50 characters or fewer.", nameof(eventType));

        if (source is { Length: > 50 })
            throw new ArgumentException("Source must be 50 characters or fewer.", nameof(source));

        return new SessionEvent
        {
            Id = Guid.NewGuid(),
            SessionId = sessionId,
            EventType = eventType,
            Timestamp = DateTime.UtcNow,
            Payload = payload ?? "{}",
            CreatedBy = createdBy,
            Source = source,
            IsDeleted = false
        };
    }

    /// <summary>
    /// Soft deletes the session event.
    /// </summary>
    public void SoftDelete()
    {
        IsDeleted = true;
        DeletedAt = DateTime.UtcNow;
    }
}
