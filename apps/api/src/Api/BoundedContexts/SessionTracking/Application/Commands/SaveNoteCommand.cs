using MediatR;

namespace Api.BoundedContexts.SessionTracking.Application.Commands;

/// <summary>
/// Command to save or update a private note.
/// </summary>
public record SaveNoteCommand(
    Guid SessionId,
    Guid ParticipantId,
    string Content,
    string? ObscuredText = null,
    Guid? NoteId = null  // If provided, update existing note
) : IRequest<SaveNoteResponse>;

/// <summary>
/// Response for SaveNoteCommand.
/// </summary>
public record SaveNoteResponse(
    Guid NoteId,
    Guid SessionId,
    Guid ParticipantId,
    bool IsRevealed,
    string? ObscuredText,
    DateTime CreatedAt,
    DateTime UpdatedAt);
