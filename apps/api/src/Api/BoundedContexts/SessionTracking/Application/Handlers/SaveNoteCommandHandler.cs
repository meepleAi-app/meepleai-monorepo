using Api.BoundedContexts.SessionTracking.Application.Commands;
using Api.BoundedContexts.SessionTracking.Domain.Entities;
using Api.BoundedContexts.SessionTracking.Domain.Repositories;
using Api.Middleware.Exceptions;
using MediatR;

namespace Api.BoundedContexts.SessionTracking.Application.Handlers;

/// <summary>
/// Handler for SaveNoteCommand.
/// </summary>
public class SaveNoteCommandHandler : IRequestHandler<SaveNoteCommand, SaveNoteResponse>
{
    private readonly ISessionNoteRepository _noteRepository;

    public SaveNoteCommandHandler(ISessionNoteRepository noteRepository)
    {
        _noteRepository = noteRepository;
    }

    public async Task<SaveNoteResponse> Handle(SaveNoteCommand request, CancellationToken cancellationToken)
    {
        SessionNote note;

        if (request.NoteId.HasValue)
        {
            // Update existing note
            note = await _noteRepository.GetByIdAsync(request.NoteId.Value, cancellationToken)
                .ConfigureAwait(false)
                ?? throw new NotFoundException($"Note with ID {request.NoteId.Value} not found.");

            // Verify ownership
            if (note.ParticipantId != request.ParticipantId)
            {
                throw new ForbiddenException("Only the note owner can update this note.");
            }

            note.UpdateContent(request.Content);
            if (request.ObscuredText is not null)
            {
                note.UpdateObscuredText(request.ObscuredText);
            }

            await _noteRepository.UpdateAsync(note, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            // Create new note
            note = SessionNote.Create(
                request.SessionId,
                request.ParticipantId,
                request.Content,
                request.ObscuredText);

            await _noteRepository.AddAsync(note, cancellationToken).ConfigureAwait(false);
        }

        await _noteRepository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return new SaveNoteResponse(
            note.Id,
            note.SessionId,
            note.ParticipantId,
            note.IsRevealed,
            note.ObscuredText,
            note.CreatedAt,
            note.UpdatedAt);
    }
}
