using MediatR;
using Api.BoundedContexts.SessionTracking.Application.Commands;
using Api.BoundedContexts.SessionTracking.Domain.Entities;
using Api.BoundedContexts.SessionTracking.Domain.Enums;
using Api.BoundedContexts.SessionTracking.Domain.Events;
using Api.BoundedContexts.SessionTracking.Domain.Repositories;
using Api.BoundedContexts.SessionTracking.Domain.Services;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.SessionTracking.Application.Commands;

/// <summary>
/// Handles updating a player's score with optimistic concurrency conflict detection.
/// On concurrent conflict, broadcasts a ConflictDetectedEvent privately to the requester
/// and throws ConflictException with the current totals for client retry.
/// Issue #4765 - Player Action Endpoints + Host Validation + Conflict Resolution
/// </summary>
public class UpdatePlayerScoreCommandHandler : IRequestHandler<UpdatePlayerScoreCommand, UpdatePlayerScoreResult>
{
    private readonly ISessionRepository _sessionRepository;
    private readonly IScoreEntryRepository _scoreEntryRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ISessionSyncService _syncService;
    private readonly ISessionBroadcastService _broadcastService;

    public UpdatePlayerScoreCommandHandler(
        ISessionRepository sessionRepository,
        IScoreEntryRepository scoreEntryRepository,
        IUnitOfWork unitOfWork,
        ISessionSyncService syncService,
        ISessionBroadcastService broadcastService)
    {
        _sessionRepository = sessionRepository ?? throw new ArgumentNullException(nameof(sessionRepository));
        _scoreEntryRepository = scoreEntryRepository ?? throw new ArgumentNullException(nameof(scoreEntryRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _syncService = syncService ?? throw new ArgumentNullException(nameof(syncService));
        _broadcastService = broadcastService ?? throw new ArgumentNullException(nameof(broadcastService));
    }

    public async Task<UpdatePlayerScoreResult> Handle(UpdatePlayerScoreCommand request, CancellationToken cancellationToken)
    {
        var session = await _sessionRepository.GetByIdAsync(request.SessionId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException($"Session {request.SessionId} not found");

        if (session.Status != SessionStatus.Active)
            throw new ConflictException($"Cannot update score for session with status {session.Status}");

        _ = session.Participants.FirstOrDefault(p => p.Id == request.ParticipantId)
            ?? throw new NotFoundException($"Participant {request.ParticipantId} not found in session");

        // Ownership check: players can only update their own score; hosts can update any participant's score
        if (request.RequesterId != request.ParticipantId)
        {
            var requester = session.Participants.FirstOrDefault(p => p.Id == request.RequesterId)
                ?? throw new NotFoundException($"Requester {request.RequesterId} not found in session");
            if (requester.Role < ParticipantRole.Host)
                throw new ForbiddenException("Players can only update their own score.");
        }

        var scoreEntry = ScoreEntry.Create(
            request.SessionId,
            request.ParticipantId,
            request.ScoreValue,
            request.RequesterId,
            request.RoundNumber,
            request.Category);

        await _scoreEntryRepository.AddAsync(scoreEntry, cancellationToken).ConfigureAwait(false);

        // Touch the session RowVersion to participate in optimistic concurrency detection
        session.UpdateAudit(request.RequesterId);
        await _sessionRepository.UpdateAsync(session, cancellationToken).ConfigureAwait(false);

        try
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateConcurrencyException)
        {
            // Conflict: notify affected player via private SSE and surface 409
            var conflictEvt = new ConflictDetectedEvent
            {
                SessionId = request.SessionId,
                AffectedUserId = request.RequesterId,
                ActionType = "score",
                Message = "Score update conflict detected. Please retry with the latest session state."
            };
            await _broadcastService.PublishAsync(
                request.SessionId,
                conflictEvt,
                EventVisibility.PrivateTo(request.RequesterId),
                cancellationToken).ConfigureAwait(false);

            throw new ConflictException(
                "Score update conflict: another participant updated the session simultaneously. Please retry.");
        }

        // Calculate updated totals and rank after successful save
        var participantScores = await _scoreEntryRepository.GetByParticipantAsync(
            request.SessionId,
            request.ParticipantId,
            cancellationToken).ConfigureAwait(false);

        var newTotal = participantScores.Sum(e => e.ScoreValue);

        var allScores = await _scoreEntryRepository.GetBySessionIdAsync(request.SessionId, cancellationToken).ConfigureAwait(false);
        var participantTotals = allScores
            .GroupBy(e => e.ParticipantId)
            .Select(g => new { ParticipantId = g.Key, Total = g.Sum(e => e.ScoreValue) })
            .OrderByDescending(x => x.Total)
            .ToList();

        var rankEntry = participantTotals
            .Select((p, index) => new { p.ParticipantId, Rank = index + 1 })
            .FirstOrDefault(x => x.ParticipantId == request.ParticipantId);
        var newRank = rankEntry?.Rank ?? participantTotals.Count + 1;

        // Broadcast public SSE event for real-time score updates
        var evt = new ScoreUpdatedEvent
        {
            SessionId = request.SessionId,
            ParticipantId = request.ParticipantId,
            ScoreEntryId = scoreEntry.Id,
            NewScore = newTotal,
            RoundNumber = request.RoundNumber,
            Category = request.Category,
            Timestamp = DateTime.UtcNow
        };
        await _syncService.PublishEventAsync(request.SessionId, evt, cancellationToken).ConfigureAwait(false);

        return new UpdatePlayerScoreResult(scoreEntry.Id, newTotal, newRank);
    }
}
