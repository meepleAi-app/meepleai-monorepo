using MediatR;

namespace Api.BoundedContexts.SessionTracking.Application.Commands;

/// <summary>
/// Command to reveal a private note to all participants.
/// </summary>
public record RevealNoteCommand(
    Guid NoteId,
    Guid SessionId,
    Guid ParticipantId  // Must be the note owner
) : IRequest<RevealNoteResponse>;

/// <summary>
/// Response for RevealNoteCommand.
/// </summary>
public record RevealNoteResponse(
    Guid NoteId,
    Guid SessionId,
    Guid ParticipantId,
    string Content,
    bool IsRevealed,
    DateTime RevealedAt);
