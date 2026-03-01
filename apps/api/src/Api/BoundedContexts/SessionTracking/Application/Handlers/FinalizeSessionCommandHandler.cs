using MediatR;
using Api.BoundedContexts.SessionTracking.Application.Commands;
using Api.BoundedContexts.SessionTracking.Domain.Entities;
using Api.BoundedContexts.SessionTracking.Domain.Events;
using Api.BoundedContexts.SessionTracking.Domain.Repositories;
using Api.BoundedContexts.SessionTracking.Domain.Services;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Infrastructure.Persistence;

namespace Api.BoundedContexts.SessionTracking.Application.Handlers;

public class FinalizeSessionCommandHandler : IRequestHandler<FinalizeSessionCommand, FinalizeSessionResult>
{
    private readonly ISessionRepository _sessionRepository;
    private readonly IScoreEntryRepository _scoreEntryRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ISessionSyncService _syncService;

    public FinalizeSessionCommandHandler(
        ISessionRepository sessionRepository,
        IScoreEntryRepository scoreEntryRepository,
        IUnitOfWork unitOfWork,
        ISessionSyncService syncService)
    {
        _sessionRepository = sessionRepository ?? throw new ArgumentNullException(nameof(sessionRepository));
        _scoreEntryRepository = scoreEntryRepository ?? throw new ArgumentNullException(nameof(scoreEntryRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _syncService = syncService ?? throw new ArgumentNullException(nameof(syncService));
    }

    public async Task<FinalizeSessionResult> Handle(FinalizeSessionCommand request, CancellationToken cancellationToken)
    {
        // Verify session exists and can be finalized
        var session = await _sessionRepository.GetByIdAsync(request.SessionId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException($"Session {request.SessionId} not found");

        if (session.Status != SessionStatus.Active && session.Status != SessionStatus.Paused)
        {
            throw new ConflictException($"Cannot finalize session with status {session.Status}");
        }

        // Verify all participants have ranks
        var allParticipantIds = session.Participants.Select(p => p.Id).ToHashSet();
        var rankedParticipantIds = request.FinalRanks.Keys.ToHashSet();

        if (!allParticipantIds.SetEquals(rankedParticipantIds))
        {
            throw new ConflictException("All participants must have a final rank");
        }

        // Get all score entries for final score calculation
        var scoreEntries = await _scoreEntryRepository.GetBySessionIdAsync(request.SessionId, cancellationToken).ConfigureAwait(false);

        // Calculate final scores
        var finalScores = scoreEntries
            .GroupBy(e => e.ParticipantId)
            .ToDictionary(
                g => g.Key,
                g => g.Sum(e => e.ScoreValue)
            );

        // Finalize session using domain method
        session.Finalize();

        // Update participant final ranks (need to access through persistence for now)
        // For now, this is a known limitation that will be addressed in refactoring

        // Save session
        await _sessionRepository.UpdateAsync(session, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        // Get winner (rank 1)
        var winnerId = request.FinalRanks
            .FirstOrDefault(kvp => kvp.Value == 1)
            .Key;

        // UserLibrary integration placeholder
        if (session.GameId != Guid.Empty)
        {
            // CreateGamesPlayedCommand integration
        }

        // GST-003: Publish SSE event for session finalization
        var durationMinutes = (int)(DateTime.UtcNow - session.SessionDate).TotalMinutes;

        var evt = new SessionFinalizedEvent
        {
            SessionId = request.SessionId,
            WinnerId = winnerId != Guid.Empty ? winnerId : null,
            FinalRanks = request.FinalRanks,
            DurationMinutes = durationMinutes,
            Timestamp = DateTime.UtcNow
        };
        await _syncService.PublishEventAsync(request.SessionId, evt, cancellationToken).ConfigureAwait(false);

        return new FinalizeSessionResult(
            winnerId != Guid.Empty ? winnerId : null,
            finalScores
        );
    }
}