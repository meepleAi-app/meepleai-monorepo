using System.Text.Json;
using MediatR;
using Api.BoundedContexts.SessionTracking.Application.Commands;
using Api.BoundedContexts.SessionTracking.Application.DTOs;
using Api.BoundedContexts.SessionTracking.Application.Services;
using Api.BoundedContexts.SessionTracking.Domain.Entities;
using Api.BoundedContexts.SessionTracking.Domain.Events;
using Api.BoundedContexts.SessionTracking.Domain.Repositories;
using Api.BoundedContexts.SessionTracking.Domain.Services;
using Api.Infrastructure;
using Api.Infrastructure.Entities.SessionTracking;
using Api.Infrastructure.Extensions;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.SessionTracking.Application.Commands;

public class FinalizeSessionCommandHandler : IRequestHandler<FinalizeSessionCommand, FinalizeSessionResult>
{
    private readonly ISessionRepository _sessionRepository;
    private readonly IScoreEntryRepository _scoreEntryRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ISessionSyncService _syncService;
    private readonly MeepleAiDbContext _db;
    private readonly TimeProvider _timeProvider;
    private readonly IDiaryStreamService _diaryStream;

    public FinalizeSessionCommandHandler(
        ISessionRepository sessionRepository,
        IScoreEntryRepository scoreEntryRepository,
        IUnitOfWork unitOfWork,
        ISessionSyncService syncService,
        MeepleAiDbContext db,
        TimeProvider timeProvider,
        IDiaryStreamService diaryStream)
    {
        _sessionRepository = sessionRepository ?? throw new ArgumentNullException(nameof(sessionRepository));
        _scoreEntryRepository = scoreEntryRepository ?? throw new ArgumentNullException(nameof(scoreEntryRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _syncService = syncService ?? throw new ArgumentNullException(nameof(syncService));
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _diaryStream = diaryStream ?? throw new ArgumentNullException(nameof(diaryStream));
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

        // Save session (adds tracked changes; SaveChangesAsync below flushes)
        await _sessionRepository.UpdateAsync(session, cancellationToken).ConfigureAwait(false);

        // Get winner (rank 1) from the FinalRanks dictionary.
        var winnerIdRaw = request.FinalRanks
            .FirstOrDefault(kvp => kvp.Value == 1)
            .Key;
        Guid? winnerIdNullable = winnerIdRaw != Guid.Empty ? winnerIdRaw : null;

        // P1B-T2: emit session_finalized diary event in the same transaction.
        // Compute scoreboard snapshot (totals per participant).
        var scoreboardSnapshot = await _db.SessionTrackingScoreEntries
            .AsNoTracking()
            .Where(e => e.SessionId == session.Id)
            .GroupBy(e => e.ParticipantId)
            .Select(g => new
            {
                participantId = g.Key,
                total = g.Sum(e => e.ScoreValue)
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        // Resolve GameNightId via the link row (if any) so the diary entry is
        // correlated with the cross-game timeline.
        var gameNightId = await _db.ResolveGameNightIdAsync(session.Id, cancellationToken).ConfigureAwait(false);

        var finalizedAt = session.FinalizedAt ?? _timeProvider.GetUtcNow().UtcDateTime;
        var durationSeconds = (int)(finalizedAt - session.SessionDate).TotalSeconds;
        if (durationSeconds < 0) durationSeconds = 0;

        var finalizedPayload = JsonSerializer.Serialize(new
        {
            winnerId = winnerIdNullable,
            durationSeconds,
            scoreboard = scoreboardSnapshot
        });

        var diaryEntity = new SessionEventEntity
        {
            Id = Guid.NewGuid(),
            SessionId = session.Id,
            GameNightId = gameNightId,
            EventType = "session_finalized",
            Timestamp = _timeProvider.GetUtcNow().UtcDateTime,
            Payload = finalizedPayload,
            CreatedBy = session.UserId,
            Source = "system",
            IsDeleted = false
        };
        _db.SessionEvents.Add(diaryEntity);

        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _diaryStream.Publish(diaryEntity.SessionId, new SessionEventDto(
            diaryEntity.Id, diaryEntity.SessionId, diaryEntity.GameNightId,
            diaryEntity.EventType, diaryEntity.Timestamp, diaryEntity.Payload,
            diaryEntity.CreatedBy, diaryEntity.Source));

        var winnerId = winnerIdRaw;

        // UserLibrary integration placeholder
        if (session.GameId != Guid.Empty)
        {
            // CreateGamesPlayedCommand integration
        }

        // GST-003: Publish SSE event for session finalization
        var durationMinutes = (int)(_timeProvider.GetUtcNow().UtcDateTime - session.SessionDate).TotalMinutes;

        var evt = new SessionFinalizedEvent
        {
            SessionId = request.SessionId,
            WinnerId = winnerId != Guid.Empty ? winnerId : null,
            FinalRanks = request.FinalRanks,
            DurationMinutes = durationMinutes,
            Timestamp = _timeProvider.GetUtcNow().UtcDateTime
        };
        await _syncService.PublishEventAsync(request.SessionId, evt, cancellationToken).ConfigureAwait(false);

        return new FinalizeSessionResult(
            winnerId != Guid.Empty ? winnerId : null,
            finalScores
        );
    }
}
