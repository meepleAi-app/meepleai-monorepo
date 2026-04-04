using Api.BoundedContexts.SessionTracking.Application.Commands;
using Api.BoundedContexts.SessionTracking.Domain.Repositories;
using Api.Middleware.Exceptions;
using MediatR;

namespace Api.BoundedContexts.SessionTracking.Application.Commands;

/// <summary>
/// Handler for RevealNoteCommand.
/// </summary>
public class RevealNoteCommandHandler : IRequestHandler<RevealNoteCommand, RevealNoteResponse>
{
    private readonly ISessionNoteRepository _noteRepository;

    public RevealNoteCommandHandler(ISessionNoteRepository noteRepository)
    {
        _noteRepository = noteRepository;
    }

    public async Task<RevealNoteResponse> Handle(RevealNoteCommand request, CancellationToken cancellationToken)
    {
        var note = await _noteRepository.GetByIdAsync(request.NoteId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException($"Note with ID {request.NoteId} not found.");

        // Verify ownership
        if (note.ParticipantId != request.ParticipantId)
        {
            throw new ForbiddenException("Only the note owner can reveal this note.");
        }

        // Verify session match
        if (note.SessionId != request.SessionId)
        {
            throw new ConflictException("Note does not belong to the specified session.");
        }

        note.Reveal();

        await _noteRepository.UpdateAsync(note, cancellationToken).ConfigureAwait(false);
        await _noteRepository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return new RevealNoteResponse(
            note.Id,
            note.SessionId,
            note.ParticipantId,
            note.GetDecryptedContent(),
            note.IsRevealed,
            note.UpdatedAt);
    }
}
