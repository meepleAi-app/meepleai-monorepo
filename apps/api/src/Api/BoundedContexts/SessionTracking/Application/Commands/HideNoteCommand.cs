using MediatR;

namespace Api.BoundedContexts.SessionTracking.Application.Commands;

/// <summary>
/// Command to hide a previously revealed note.
/// </summary>
public record HideNoteCommand(
    Guid NoteId,
    Guid SessionId,
    Guid ParticipantId  // Must be the note owner
) : IRequest<HideNoteResponse>;

/// <summary>
/// Response for HideNoteCommand.
/// </summary>
public record HideNoteResponse(
    Guid NoteId,
    Guid SessionId,
    Guid ParticipantId,
    bool IsRevealed,
    DateTime HiddenAt);
