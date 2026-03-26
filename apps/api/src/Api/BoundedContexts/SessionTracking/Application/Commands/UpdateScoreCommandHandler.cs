using MediatR;
using Api.BoundedContexts.SessionTracking.Application.Commands;
using Api.BoundedContexts.SessionTracking.Application.DTOs;
using Api.BoundedContexts.SessionTracking.Domain.Entities;
using Api.BoundedContexts.SessionTracking.Domain.Events;
using Api.BoundedContexts.SessionTracking.Domain.Repositories;
using Api.BoundedContexts.SessionTracking.Domain.Services;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Infrastructure.Persistence;

namespace Api.BoundedContexts.SessionTracking.Application.Commands;

public class UpdateScoreCommandHandler : IRequestHandler<UpdateScoreCommand, UpdateScoreResult>
{
    private readonly ISessionRepository _sessionRepository;
    private readonly IScoreEntryRepository _scoreEntryRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ISessionSyncService _syncService;

    public UpdateScoreCommandHandler(
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

    public async Task<UpdateScoreResult> Handle(UpdateScoreCommand request, CancellationToken cancellationToken)
    {
        // Verify session exists and is active
        var session = await _sessionRepository.GetByIdAsync(request.SessionId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException($"Session {request.SessionId} not found");

        if (session.Status != SessionStatus.Active)
        {
            throw new ConflictException($"Cannot update score for session with status {session.Status}");
        }

        // Verify participant belongs to session
        _ = session.Participants.FirstOrDefault(p => p.Id == request.ParticipantId)
            ?? throw new NotFoundException($"Participant {request.ParticipantId} not found in session");

        // Create score entry with correct parameter order
        var scoreEntry = ScoreEntry.Create(
            request.SessionId,
            request.ParticipantId,
            request.ScoreValue,
            session.UserId, // createdBy
            request.RoundNumber,
            request.Category
        );

        // Add score entry via repository
        await _scoreEntryRepository.AddAsync(scoreEntry, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        // Calculate updated totals and rank
        var participantScores = await _scoreEntryRepository.GetByParticipantAsync(
            request.SessionId,
            request.ParticipantId,
            cancellationToken).ConfigureAwait(false);

        var newTotal = participantScores.Sum(e => e.ScoreValue);

        // Get all participant scores for ranking
        var allScores = await _scoreEntryRepository.GetBySessionIdAsync(request.SessionId, cancellationToken).ConfigureAwait(false);
        var participantTotals = allScores
            .GroupBy(e => e.ParticipantId)
            .Select(g => new { ParticipantId = g.Key, Total = g.Sum(e => e.ScoreValue) })
            .OrderByDescending(x => x.Total)
            .ToList();

        var newRank = participantTotals
            .Select((p, index) => new { p.ParticipantId, Rank = index + 1 })
            .First(x => x.ParticipantId == request.ParticipantId)
            .Rank;

        // GST-003: Publish SSE event for real-time score updates
        var evt = new ScoreUpdatedEvent
        {
            SessionId = request.SessionId,
            ParticipantId = request.ParticipantId,
            ScoreEntryId = scoreEntry.Id,
            NewScore = request.ScoreValue,
            RoundNumber = request.RoundNumber,
            Category = request.Category,
            Timestamp = DateTime.UtcNow
        };
        await _syncService.PublishEventAsync(request.SessionId, evt, cancellationToken).ConfigureAwait(false);

        return new UpdateScoreResult(
            scoreEntry.Id,
            newTotal,
            newRank
        );
    }
}
