using Api.BoundedContexts.SessionTracking.Application.Commands;
using Api.BoundedContexts.SessionTracking.Domain.Events;
using Api.BoundedContexts.SessionTracking.Domain.Repositories;
using Api.BoundedContexts.SessionTracking.Domain.Services;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Infrastructure.Persistence;
using MediatR;

namespace Api.BoundedContexts.SessionTracking.Application.Handlers;

/// <summary>
/// Handles kicking a participant from a session. Host-only action.
/// Issue #4765 - Player Action Endpoints + Host Validation
/// </summary>
public class KickParticipantCommandHandler : IRequestHandler<KickParticipantCommand, KickParticipantResult>
{
    private readonly ISessionRepository _sessionRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ISessionSyncService _syncService;

    public KickParticipantCommandHandler(
        ISessionRepository sessionRepository,
        IUnitOfWork unitOfWork,
        ISessionSyncService syncService)
    {
        _sessionRepository = sessionRepository ?? throw new ArgumentNullException(nameof(sessionRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _syncService = syncService ?? throw new ArgumentNullException(nameof(syncService));
    }

    public async Task<KickParticipantResult> Handle(
        KickParticipantCommand request,
        CancellationToken cancellationToken)
    {
        var session = await _sessionRepository.GetByIdAsync(request.SessionId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException($"Session {request.SessionId} not found");

        // Get participant info before removal
        var participant = session.Participants.FirstOrDefault(p => p.Id == request.ParticipantId)
            ?? throw new NotFoundException($"Participant {request.ParticipantId} not found in session.");

        var displayName = participant.DisplayName;

        // Domain logic handles all validation (finalized, can't kick host)
        session.RemoveParticipant(request.ParticipantId);

        await _sessionRepository.UpdateAsync(session, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var evt = new ParticipantKickedEvent
        {
            SessionId = request.SessionId,
            ParticipantId = request.ParticipantId,
            DisplayName = displayName,
            KickedBy = request.RequesterId,
            Timestamp = DateTime.UtcNow
        };
        await _syncService.PublishEventAsync(request.SessionId, evt, cancellationToken).ConfigureAwait(false);

        return new KickParticipantResult(request.ParticipantId, displayName);
    }
}
