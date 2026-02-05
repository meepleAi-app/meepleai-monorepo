using Api.BoundedContexts.SessionTracking.Application.Queries;
using Api.BoundedContexts.SessionTracking.Domain.Repositories;
using MediatR;

namespace Api.BoundedContexts.SessionTracking.Application.Handlers;

/// <summary>
/// Handler for GetSessionNotesQuery.
/// </summary>
public class GetSessionNotesQueryHandler : IRequestHandler<GetSessionNotesQuery, GetSessionNotesResponse>
{
    private readonly ISessionNoteRepository _noteRepository;

    public GetSessionNotesQueryHandler(ISessionNoteRepository noteRepository)
    {
        _noteRepository = noteRepository;
    }

    public async Task<GetSessionNotesResponse> Handle(GetSessionNotesQuery request, CancellationToken cancellationToken)
    {
        var notes = await _noteRepository.GetVisibleNotesAsync(
            request.SessionId,
            request.RequesterId,
            cancellationToken)
            .ConfigureAwait(false);

        var dtos = notes.Select(note => new SessionNoteDto(
            note.Id,
            note.SessionId,
            note.ParticipantId,
            note.CanView(request.RequesterId) ? note.GetDecryptedContent() : null,
            note.ParticipantId == request.RequesterId,
            note.IsRevealed,
            note.ObscuredText,
            note.CreatedAt,
            note.UpdatedAt))
            .ToList();

        return new GetSessionNotesResponse(request.SessionId, dtos);
    }
}

/// <summary>
/// Handler for GetNoteByIdQuery.
/// </summary>
public class GetNoteByIdQueryHandler : IRequestHandler<GetNoteByIdQuery, SessionNoteDto?>
{
    private readonly ISessionNoteRepository _noteRepository;

    public GetNoteByIdQueryHandler(ISessionNoteRepository noteRepository)
    {
        _noteRepository = noteRepository;
    }

    public async Task<SessionNoteDto?> Handle(GetNoteByIdQuery request, CancellationToken cancellationToken)
    {
        var note = await _noteRepository.GetByIdAsync(request.NoteId, cancellationToken)
            .ConfigureAwait(false);

        if (note is null)
        {
            return null;
        }

        // Only return if requester can view it
        if (!note.CanView(request.RequesterId))
        {
            return null;
        }

        return new SessionNoteDto(
            note.Id,
            note.SessionId,
            note.ParticipantId,
            note.CanView(request.RequesterId) ? note.GetDecryptedContent() : null,
            note.ParticipantId == request.RequesterId,
            note.IsRevealed,
            note.ObscuredText,
            note.CreatedAt,
            note.UpdatedAt);
    }
}

/// <summary>
/// Handler for GetParticipantNotesQuery.
/// </summary>
public class GetParticipantNotesQueryHandler : IRequestHandler<GetParticipantNotesQuery, GetParticipantNotesResponse>
{
    private readonly ISessionNoteRepository _noteRepository;

    public GetParticipantNotesQueryHandler(ISessionNoteRepository noteRepository)
    {
        _noteRepository = noteRepository;
    }

    public async Task<GetParticipantNotesResponse> Handle(GetParticipantNotesQuery request, CancellationToken cancellationToken)
    {
        var notes = await _noteRepository.GetByParticipantIdAsync(
            request.SessionId,
            request.ParticipantId,
            cancellationToken)
            .ConfigureAwait(false);

        // Filter to only notes the requester can view
        var visibleNotes = notes
            .Where(n => n.CanView(request.RequesterId))
            .Select(note => new SessionNoteDto(
                note.Id,
                note.SessionId,
                note.ParticipantId,
                note.CanView(request.RequesterId) ? note.GetDecryptedContent() : null,
                note.ParticipantId == request.RequesterId,
                note.IsRevealed,
                note.ObscuredText,
                note.CreatedAt,
                note.UpdatedAt))
            .ToList();

        return new GetParticipantNotesResponse(request.SessionId, request.ParticipantId, visibleNotes);
    }
}
