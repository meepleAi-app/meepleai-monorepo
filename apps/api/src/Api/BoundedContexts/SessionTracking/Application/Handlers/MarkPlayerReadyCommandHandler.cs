using Api.BoundedContexts.SessionTracking.Application.Commands;
using Api.BoundedContexts.SessionTracking.Domain.Entities;
using Api.BoundedContexts.SessionTracking.Domain.Events;
using Api.BoundedContexts.SessionTracking.Domain.Repositories;
using Api.BoundedContexts.SessionTracking.Domain.Services;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Infrastructure.Persistence;
using MediatR;

namespace Api.BoundedContexts.SessionTracking.Application.Handlers;

/// <summary>
/// Handles marking a player as ready for the next phase/turn.
/// Issue #4765 - Player Action Endpoints
/// </summary>
public class MarkPlayerReadyCommandHandler : IRequestHandler<MarkPlayerReadyCommand, MarkPlayerReadyResult>
{
    private readonly ISessionRepository _sessionRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ISessionSyncService _syncService;

    public MarkPlayerReadyCommandHandler(
        ISessionRepository sessionRepository,
        IUnitOfWork unitOfWork,
        ISessionSyncService syncService)
    {
        _sessionRepository = sessionRepository ?? throw new ArgumentNullException(nameof(sessionRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _syncService = syncService ?? throw new ArgumentNullException(nameof(syncService));
    }

    public async Task<MarkPlayerReadyResult> Handle(
        MarkPlayerReadyCommand request,
        CancellationToken cancellationToken)
    {
        var session = await _sessionRepository.GetByIdAsync(request.SessionId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException($"Session {request.SessionId} not found");

        if (session.Status != SessionStatus.Active)
            throw new ConflictException($"Cannot mark ready in session with status {session.Status}.");

        session.MarkParticipantReady(request.ParticipantId);

        await _sessionRepository.UpdateAsync(session, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var participant = session.Participants.First(p => p.Id == request.ParticipantId);
        var activePlayers = session.Participants.Where(p => p.Role != Domain.Enums.ParticipantRole.Spectator).ToList();
        var readyCount = activePlayers.Count(p => p.IsReady);

        var evt = new PlayerReadyEvent
        {
            SessionId = request.SessionId,
            ParticipantId = request.ParticipantId,
            DisplayName = participant.DisplayName,
            IsReady = true,
            ReadyCount = readyCount,
            TotalPlayers = activePlayers.Count,
            Timestamp = DateTime.UtcNow
        };
        await _syncService.PublishEventAsync(request.SessionId, evt, cancellationToken).ConfigureAwait(false);

        return new MarkPlayerReadyResult(true, readyCount, activePlayers.Count);
    }
}
