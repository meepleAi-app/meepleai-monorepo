using MediatR;

namespace Api.BoundedContexts.SessionTracking.Application.Commands;

/// <summary>
/// Command to delete a private note.
/// </summary>
public record DeleteNoteCommand(
    Guid NoteId,
    Guid SessionId,
    Guid ParticipantId  // Must be the note owner
) : IRequest<DeleteNoteResponse>;

/// <summary>
/// Response for DeleteNoteCommand.
/// </summary>
public record DeleteNoteResponse(
    Guid NoteId,
    bool Deleted,
    DateTime DeletedAt);
