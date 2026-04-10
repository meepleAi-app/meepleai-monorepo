using System.Text.Json;
using Api.BoundedContexts.SessionTracking.Application.DTOs;
using Api.BoundedContexts.SessionTracking.Application.Services;
using Api.Infrastructure;
using Api.Infrastructure.Entities.SessionTracking;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.GameManagement.Application.Commands;

/// <summary>
/// Session Flow v2.1 — Plan 1bis T3.
/// Handles <see cref="CompleteGameNightCommand"/>: cascade-finalizes all
/// non-finalized sessions in the game night, marks link rows as Completed,
/// transitions the <c>GameNightEvent</c> entity to <c>Completed</c>, and
/// emits diary events (<c>session_finalized</c> per session +
/// <c>gamenight_completed</c> once).
///
/// Works at the persistence layer (EF entities) because the
/// <c>GameNightEvent</c> aggregate is <c>internal sealed</c> in the
/// GameManagement domain and the handler needs cross-BC coordination
/// with SessionTracking entities.
/// </summary>
internal sealed class CompleteGameNightCommandHandler
    : IRequestHandler<CompleteGameNightCommand, CompleteGameNightResult>
{
    private readonly MeepleAiDbContext _db;
    private readonly IUnitOfWork _unitOfWork;
    private readonly TimeProvider _timeProvider;
    private readonly IDiaryStreamService _diaryStream;

    public CompleteGameNightCommandHandler(MeepleAiDbContext db, IUnitOfWork unitOfWork, TimeProvider timeProvider, IDiaryStreamService diaryStream)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _diaryStream = diaryStream ?? throw new ArgumentNullException(nameof(diaryStream));
    }

    public async Task<CompleteGameNightResult> Handle(
        CompleteGameNightCommand request,
        CancellationToken cancellationToken)
    {
        // ── 1. Load GameNightEvent entity ──────────────────────────────
        var nightEntity = await _db.GameNightEvents
            .FirstOrDefaultAsync(e => e.Id == request.GameNightEventId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException(
                $"GameNightEvent {request.GameNightEventId} not found.");

        if (nightEntity.OrganizerId != request.UserId)
            throw new ForbiddenException("Only the night organizer can complete it.");

        if (!string.Equals(nightEntity.Status, "InProgress", StringComparison.Ordinal))
            throw new ConflictException(
                $"GameNightEvent is in status '{nightEntity.Status}', cannot complete ad-hoc.");

        // ── 2. Discover sessions linked to this night ──────────────────
        var links = await _db.GameNightSessions
            .Where(gns => gns.GameNightEventId == nightEntity.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var linkSessionIds = links.Select(l => l.SessionId).ToList();
        var sessionCount = linkSessionIds.Count;

        // ── 3. Cascade-finalize non-finalized sessions ─────────────────
#pragma warning disable MA0006 // EF LINQ expression — string.Equals does not translate to SQL
        var nonFinalizedSessions = await _db.SessionTrackingSessions
            .Where(s => linkSessionIds.Contains(s.Id) && s.Status != "Finalized")
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
#pragma warning restore MA0006

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var finalizedCount = 0;
        var diaryEntities = new List<SessionEventEntity>();

        foreach (var session in nonFinalizedSessions)
        {
            session.Status = "Finalized";
            session.FinalizedAt = now;
            session.UpdatedAt = now;
            finalizedCount++;

            var durationSec = (int)(now - session.SessionDate).TotalSeconds;
            if (durationSec < 0) durationSec = 0;

            var sessionDiary = new SessionEventEntity
            {
                Id = Guid.NewGuid(),
                SessionId = session.Id,
                GameNightId = nightEntity.Id,
                EventType = "session_finalized",
                Timestamp = now,
                Payload = JsonSerializer.Serialize(new
                {
                    winnerId = (Guid?)null,
                    reason = "cascade_from_gamenight_complete",
                    durationSeconds = durationSec
                }),
                CreatedBy = request.UserId,
                Source = "system",
                IsDeleted = false
            };
            _db.SessionEvents.Add(sessionDiary);
            diaryEntities.Add(sessionDiary);
        }

        // ── 4. Mark GameNightSession links as Completed ────────────────
#pragma warning disable MA0006 // In-memory LINQ on materialized list — ordinal comparison is fine
        foreach (var link in links.Where(l => l.Status != "Completed"))
#pragma warning restore MA0006
        {
            link.Status = "Completed";
            link.CompletedAt = _timeProvider.GetUtcNow();
        }

        // ── 5. Transition GameNight to Completed ───────────────────────
        nightEntity.Status = "Completed";
        nightEntity.UpdatedAt = _timeProvider.GetUtcNow();

        // ── 6. Emit gamenight_completed diary event ────────────────────
        var durationSeconds = (int)(now - nightEntity.CreatedAt).TotalSeconds;
        if (durationSeconds < 0) durationSeconds = 0;

        var anchorSessionId = linkSessionIds.FirstOrDefault();
        if (anchorSessionId != Guid.Empty)
        {
            var nightDiary = new SessionEventEntity
            {
                Id = Guid.NewGuid(),
                SessionId = anchorSessionId,
                GameNightId = nightEntity.Id,
                EventType = "gamenight_completed",
                Timestamp = now,
                Payload = JsonSerializer.Serialize(new
                {
                    gameNightEventId = nightEntity.Id,
                    sessionCount,
                    finalizedSessionCount = finalizedCount,
                    durationSeconds
                }),
                CreatedBy = request.UserId,
                Source = "system",
                IsDeleted = false
            };
            _db.SessionEvents.Add(nightDiary);
            diaryEntities.Add(nightDiary);
        }

        // ── 7. Atomic save ─────────────────────────────────────────────
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        // Publish diary events to SSE stream after successful save.
        foreach (var de in diaryEntities)
        {
            _diaryStream.Publish(de.SessionId, new SessionEventDto(
                de.Id, de.SessionId, de.GameNightId,
                de.EventType, de.Timestamp, de.Payload,
                de.CreatedBy, de.Source));
        }

        return new CompleteGameNightResult(
            GameNightEventId: nightEntity.Id,
            SessionCount: sessionCount,
            FinalizedSessionCount: finalizedCount,
            DurationSeconds: durationSeconds);
    }
}
