namespace Api.BoundedContexts.SessionTracking.Domain.Events;

/// <summary>
/// Event raised when a participant saves a note.
/// This event is only broadcast to inform that a note exists, not its content.
/// </summary>
public record NoteSavedEvent(
    Guid NoteId,
    Guid SessionId,
    Guid ParticipantId,
    string ParticipantName,
    bool HasObscuredText,
    DateTime Timestamp);

/// <summary>
/// Event raised when a participant reveals their note to all.
/// </summary>
public record NoteRevealedEvent(
    Guid NoteId,
    Guid SessionId,
    Guid ParticipantId,
    string ParticipantName,
    string Content,
    DateTime Timestamp);

/// <summary>
/// Event raised when a participant hides their note again.
/// </summary>
public record NoteHiddenEvent(
    Guid NoteId,
    Guid SessionId,
    Guid ParticipantId,
    string ParticipantName,
    DateTime Timestamp);

/// <summary>
/// Event raised when a participant updates their note.
/// </summary>
public record NoteUpdatedEvent(
    Guid NoteId,
    Guid SessionId,
    Guid ParticipantId,
    string ParticipantName,
    string? ObscuredText,
    bool IsRevealed,
    string? Content,  // Only included if revealed
    DateTime Timestamp);

/// <summary>
/// Event raised when a participant deletes their note.
/// </summary>
public record NoteDeletedEvent(
    Guid NoteId,
    Guid SessionId,
    Guid ParticipantId,
    string ParticipantName,
    DateTime Timestamp);
