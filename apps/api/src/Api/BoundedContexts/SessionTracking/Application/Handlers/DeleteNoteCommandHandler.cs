using Api.BoundedContexts.SessionTracking.Application.Commands;
using Api.BoundedContexts.SessionTracking.Domain.Repositories;
using Api.Middleware.Exceptions;
using MediatR;

namespace Api.BoundedContexts.SessionTracking.Application.Handlers;

/// <summary>
/// Handler for DeleteNoteCommand.
/// </summary>
public class DeleteNoteCommandHandler : IRequestHandler<DeleteNoteCommand, DeleteNoteResponse>
{
    private readonly ISessionNoteRepository _noteRepository;

    public DeleteNoteCommandHandler(ISessionNoteRepository noteRepository)
    {
        _noteRepository = noteRepository;
    }

    public async Task<DeleteNoteResponse> Handle(DeleteNoteCommand request, CancellationToken cancellationToken)
    {
        var note = await _noteRepository.GetByIdAsync(request.NoteId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException($"Note with ID {request.NoteId} not found.");

        // Verify ownership
        if (note.ParticipantId != request.ParticipantId)
        {
            throw new ForbiddenException("Only the note owner can delete this note.");
        }

        // Verify session match
        if (note.SessionId != request.SessionId)
        {
            throw new ConflictException("Note does not belong to the specified session.");
        }

        note.SoftDelete();

        await _noteRepository.UpdateAsync(note, cancellationToken).ConfigureAwait(false);
        await _noteRepository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return new DeleteNoteResponse(
            note.Id,
            true,
            note.DeletedAt!.Value);
    }
}
