using MediatR;

namespace Api.BoundedContexts.SessionTracking.Application.Commands;

public record AddNoteCommand(
    Guid SessionId,
    Guid ParticipantId,
    string NoteType, // 'Private' | 'Shared' | 'Template'
    string? TemplateKey,
    string Content,
    bool IsHidden,
    Guid RequestedBy // IDOR guard: authenticated caller — must be the session owner or a participant.
) : IRequest<AddNoteResult>;

public record AddNoteResult(
    Guid NoteId
);
