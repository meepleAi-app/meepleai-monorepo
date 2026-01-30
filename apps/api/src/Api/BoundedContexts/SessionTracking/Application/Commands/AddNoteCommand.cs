using MediatR;

namespace Api.BoundedContexts.SessionTracking.Application.Commands;

public record AddNoteCommand(
    Guid SessionId,
    Guid ParticipantId,
    string NoteType, // 'Private' | 'Shared' | 'Template'
    string? TemplateKey,
    string Content,
    bool IsHidden
) : IRequest<AddNoteResult>;

public record AddNoteResult(
    Guid NoteId
);
