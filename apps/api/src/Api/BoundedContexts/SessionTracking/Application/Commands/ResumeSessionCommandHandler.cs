using Api.BoundedContexts.GameManagement.Domain.Enums;
using Api.BoundedContexts.SessionTracking.Application.DTOs;
using Api.BoundedContexts.SessionTracking.Application.Services;
using Api.BoundedContexts.SessionTracking.Domain.Entities;
using Api.BoundedContexts.SessionTracking.Domain.Repositories;
using Api.Infrastructure;
using Api.Infrastructure.Entities.SessionTracking;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.SessionTracking.Application.Commands;

/// <summary>
/// Session Flow v2.1 — T5.
/// Loads the session via <see cref="ISessionRepository"/>, applies the
/// domain <see cref="Session.Resume"/> transition, and writes a
/// <c>session_resumed</c> diary entry. Before activating the requested session,
/// it auto-pauses every other in-progress sibling session inside the same
/// GameNight envelope (each gets its own <c>session_paused</c> diary entry with
/// <c>reason=auto_pause_on_resume</c>) and frees the link slot held in
/// <c>game_night_sessions</c> so the partial unique index from T1 keeps the
/// "1 Active per GameNight" invariant intact.
/// </summary>
internal sealed class ResumeSessionCommandHandler : ICommandHandler<ResumeSessionCommand>
{
    private readonly ISessionRepository _sessionRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly MeepleAiDbContext _db;
    private readonly TimeProvider _timeProvider;
    private readonly IDiaryStreamService _diaryStream;

    public ResumeSessionCommandHandler(
        ISessionRepository sessionRepository,
        IUnitOfWork unitOfWork,
        MeepleAiDbContext db,
        TimeProvider timeProvider,
        IDiaryStreamService diaryStream)
    {
        _sessionRepository = sessionRepository ?? throw new ArgumentNullException(nameof(sessionRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _diaryStream = diaryStream ?? throw new ArgumentNullException(nameof(diaryStream));
    }

    public async Task Handle(ResumeSessionCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var session = await _sessionRepository
            .GetByIdAsync(request.SessionId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException($"Session {request.SessionId} not found.");

        if (session.UserId != request.UserId)
        {
            throw new ForbiddenException("Only the session owner can resume this session.");
        }

        // Load this session's link row (may be null for unattached/solo sessions).
        var ownLinkRow = await _db.GameNightSessions
            .FirstOrDefaultAsync(gns => gns.SessionId == session.Id, cancellationToken)
            .ConfigureAwait(false);

        Guid? gameNightId = ownLinkRow?.GameNightEventId;

        // Wrap ALL mutations in an explicit transaction so that auto-pausing siblings
        // and resuming the target session commit (or roll back) atomically.
        // The two SaveChangesAsync calls are still needed to satisfy the partial unique
        // index (the first flush clears sibling InProgress rows before the second flush
        // sets this session's row to InProgress), but both live inside a single
        // transaction — if the second save fails, the first is rolled back too.
        await _unitOfWork.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        // Collect diary entities for SSE publishing after successful commit.
        var diaryEntities = new List<SessionEventEntity>();

        try
        {
            // Invariant guard: free every other "InProgress" link slot in the same GameNight
            // BEFORE flipping our own row to InProgress, so the partial unique index from T1
            // never sees two InProgress rows at commit time.
            if (gameNightId.HasValue)
            {
                var otherInProgressLinks = await _db.GameNightSessions
                    .Where(gns =>
                        gns.GameNightEventId == gameNightId.Value &&
                        gns.SessionId != session.Id &&
                        gns.Status == nameof(GameNightSessionStatus.InProgress))
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);

                if (otherInProgressLinks.Count > 0)
                {
                    foreach (var otherLink in otherInProgressLinks)
                    {
                        // Free the link slot first — the unique index is the hard invariant.
                        otherLink.Status = GameNightSessionStatus.Pending.ToString();

                        var other = await _sessionRepository
                            .GetByIdAsync(otherLink.SessionId, cancellationToken)
                            .ConfigureAwait(false);

                        if (other is null || other.Status != SessionStatus.Active)
                        {
                            continue;
                        }

                        other.Pause();
                        await _sessionRepository.UpdateAsync(other, cancellationToken).ConfigureAwait(false);

                        var siblingDiary = new SessionEventEntity
                        {
                            Id = Guid.NewGuid(),
                            SessionId = other.Id,
                            GameNightId = gameNightId,
                            EventType = "session_paused",
                            Timestamp = _timeProvider.GetUtcNow().UtcDateTime,
                            Payload = "{\"reason\":\"auto_pause_on_resume\"}",
                            CreatedBy = request.UserId,
                            Source = "system",
                            IsDeleted = false
                        };
                        _db.SessionEvents.Add(siblingDiary);
                        diaryEntities.Add(siblingDiary);
                    }

                    // Flush auto-pauses so the partial unique index sees zero InProgress
                    // rows before the next flush adds ours back.
                    await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                }
            }

            // Domain rule: throws ConflictException when not Paused.
            session.Resume();
            await _sessionRepository.UpdateAsync(session, cancellationToken).ConfigureAwait(false);

            // Reclaim the link slot for the resumed session.
            if (ownLinkRow is not null)
            {
                ownLinkRow.Status = GameNightSessionStatus.InProgress.ToString();
            }

            var resumeDiary = new SessionEventEntity
            {
                Id = Guid.NewGuid(),
                SessionId = session.Id,
                GameNightId = gameNightId,
                EventType = "session_resumed",
                Timestamp = _timeProvider.GetUtcNow().UtcDateTime,
                Payload = "{}",
                CreatedBy = request.UserId,
                Source = "system",
                IsDeleted = false
            };
            _db.SessionEvents.Add(resumeDiary);
            diaryEntities.Add(resumeDiary);

            await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            await _unitOfWork.CommitTransactionAsync(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync(cancellationToken).ConfigureAwait(false);
            throw;
        }

        // Publish all diary events after successful commit (best-effort, fire-and-forget).
        foreach (var de in diaryEntities)
        {
            _diaryStream.Publish(de.SessionId, new SessionEventDto(
                de.Id, de.SessionId, de.GameNightId,
                de.EventType, de.Timestamp, de.Payload,
                de.CreatedBy, de.Source));
        }
    }
}
