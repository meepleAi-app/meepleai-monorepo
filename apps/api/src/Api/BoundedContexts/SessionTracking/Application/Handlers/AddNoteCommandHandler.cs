using MediatR;
using Api.BoundedContexts.SessionTracking.Application.Commands;
using Api.BoundedContexts.SessionTracking.Domain.Entities;
using Api.BoundedContexts.SessionTracking.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.Infrastructure;
using Api.Infrastructure.Entities.SessionTracking;
using Api.SharedKernel.Infrastructure.Persistence;

namespace Api.BoundedContexts.SessionTracking.Application.Handlers;

public class AddNoteCommandHandler : IRequestHandler<AddNoteCommand, AddNoteResult>
{
    private readonly ISessionRepository _sessionRepository;
    private readonly MeepleAiDbContext _context;
    private readonly IUnitOfWork _unitOfWork;

    public AddNoteCommandHandler(
        ISessionRepository sessionRepository,
        MeepleAiDbContext context,
        IUnitOfWork unitOfWork)
    {
        _sessionRepository = sessionRepository ?? throw new ArgumentNullException(nameof(sessionRepository));
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    }

    public async Task<AddNoteResult> Handle(AddNoteCommand request, CancellationToken cancellationToken)
    {
        // Verify session exists
        var session = await _sessionRepository.GetByIdAsync(request.SessionId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException($"Session {request.SessionId} not found");

        // Verify participant exists in session
        _ = session.Participants.FirstOrDefault(p => p.Id == request.ParticipantId)
            ?? throw new NotFoundException($"Participant {request.ParticipantId} not found in session");

        // Parse note type enum
        var noteType = Enum.Parse<NoteType>(request.NoteType);

        // Create note domain entity
        var note = PlayerNote.Create(
            request.SessionId,
            request.ParticipantId,
            noteType,
            request.Content,
            request.TemplateKey,
            request.IsHidden
        );

        // Map to persistence entity and save directly
        var noteEntity = new PlayerNoteEntity
        {
            Id = note.Id,
            SessionId = note.SessionId,
            ParticipantId = note.ParticipantId,
            NoteType = note.NoteType.ToString(),
            Content = note.Content,
            TemplateKey = note.TemplateKey,
            IsHidden = note.IsHidden,
            CreatedAt = note.CreatedAt,
            UpdatedAt = null
        };

        _context.SessionTrackingPlayerNotes.Add(noteEntity);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return new AddNoteResult(note.Id);
    }
}