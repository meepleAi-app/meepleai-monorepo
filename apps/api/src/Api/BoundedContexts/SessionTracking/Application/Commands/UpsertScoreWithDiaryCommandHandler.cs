using System.Text.Json;
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
/// Session Flow v2.1 — T8 handler.
/// Loads (or creates) a <see cref="ScoreEntryEntity"/> row scoped to
/// <c>(SessionId, ParticipantId, RoundNumber?, Category?)</c>, mutates
/// <c>ScoreValue</c> last-write-wins, and emits a <c>score_updated</c> diary
/// event carrying <c>{oldValue, newValue, reason, …}</c> so the full change
/// history is preserved on the append-only <c>SessionEvents</c> stream.
/// </summary>
internal sealed class UpsertScoreWithDiaryCommandHandler
    : ICommandHandler<UpsertScoreWithDiaryCommand, UpsertScoreWithDiaryResult>
{
    private const string DefaultCategory = "total";

    private readonly ISessionRepository _sessionRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly MeepleAiDbContext _db;

    public UpsertScoreWithDiaryCommandHandler(
        ISessionRepository sessionRepository,
        IUnitOfWork unitOfWork,
        MeepleAiDbContext db)
    {
        _sessionRepository = sessionRepository ?? throw new ArgumentNullException(nameof(sessionRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _db = db ?? throw new ArgumentNullException(nameof(db));
    }

    public async Task<UpsertScoreWithDiaryResult> Handle(
        UpsertScoreWithDiaryCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var session = await _sessionRepository
            .GetByIdAsync(request.SessionId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException($"Session {request.SessionId} not found.");

        if (session.UserId != request.RequesterId)
        {
            throw new ForbiddenException("Only the session owner can update scores.");
        }

        var participantExists = session.Participants.Any(p => p.Id == request.ParticipantId);
        if (!participantExists)
        {
            throw new NotFoundException(
                $"Participant {request.ParticipantId} not found in session {request.SessionId}.");
        }

        // Default to the "total" category when neither RoundNumber nor Category is
        // provided. The domain ScoreEntry.Create factory (used below for argument
        // validation parity) requires at least one of the two; this mirrors §4.6.
        var effectiveRound = request.RoundNumber;
        var effectiveCategory = string.IsNullOrWhiteSpace(request.Category)
            ? (effectiveRound.HasValue ? null : DefaultCategory)
            : request.Category!.Trim();

        // Locate the existing projection row (last-write-wins upsert scope).
        var existing = await _db.SessionTrackingScoreEntries
            .FirstOrDefaultAsync(
                e => e.SessionId == request.SessionId
                  && e.ParticipantId == request.ParticipantId
                  && e.RoundNumber == effectiveRound
                  && e.Category == effectiveCategory,
                cancellationToken)
            .ConfigureAwait(false);

        decimal oldValue;
        Guid entryId;

        if (existing is null)
        {
            // Validate arguments by funnelling them through the domain factory; then
            // persist via the entity (the domain type is Ignore()d in the DbContext).
            var domain = ScoreEntry.Create(
                sessionId: request.SessionId,
                participantId: request.ParticipantId,
                scoreValue: request.NewValue,
                createdBy: request.RequesterId,
                roundNumber: effectiveRound,
                category: effectiveCategory);

            var entity = new ScoreEntryEntity
            {
                Id = domain.Id,
                SessionId = domain.SessionId,
                ParticipantId = domain.ParticipantId,
                RoundNumber = domain.RoundNumber,
                Category = domain.Category,
                ScoreValue = domain.ScoreValue,
                Timestamp = domain.Timestamp,
                CreatedBy = domain.CreatedBy
            };

            _db.SessionTrackingScoreEntries.Add(entity);
            oldValue = 0m;
            entryId = entity.Id;
        }
        else
        {
            oldValue = existing.ScoreValue;
            existing.ScoreValue = request.NewValue;
            existing.Timestamp = DateTime.UtcNow;
            entryId = existing.Id;
        }

        // Correlate the diary event with the parent GameNight envelope (if any).
        var gameNightId = await _db.GameNightSessions
            .Where(gns => gns.SessionId == request.SessionId)
            .Select(gns => (Guid?)gns.GameNightEventId)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        var payload = JsonSerializer.Serialize(new
        {
            scoreEntryId = entryId,
            participantId = request.ParticipantId,
            oldValue,
            newValue = request.NewValue,
            roundNumber = effectiveRound,
            category = effectiveCategory,
            reason = request.Reason
        });

        _db.SessionEvents.Add(new SessionEventEntity
        {
            Id = Guid.NewGuid(),
            SessionId = request.SessionId,
            GameNightId = gameNightId,
            EventType = "score_updated",
            Timestamp = DateTime.UtcNow,
            Payload = payload,
            CreatedBy = request.RequesterId,
            Source = "user",
            IsDeleted = false
        });

        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return new UpsertScoreWithDiaryResult(entryId, oldValue, request.NewValue);
    }
}
