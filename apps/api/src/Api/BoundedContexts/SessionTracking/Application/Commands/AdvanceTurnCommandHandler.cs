using System.Text.Json;
using Api.BoundedContexts.SessionTracking.Domain.Repositories;
using Api.Infrastructure;
using Api.Infrastructure.Entities.SessionTracking;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Application.Interfaces;
using Api.Infrastructure.Extensions;
using Api.SharedKernel.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.SessionTracking.Application.Commands;

/// <summary>
/// Session Flow v2.1 — Plan 1bis T1.
/// Loads the session via <see cref="ISessionRepository"/>, invokes the
/// domain <c>AdvanceTurn</c> transition, persists the updated
/// <c>CurrentTurnIndex</c>, and appends a <c>turn_advanced</c> diary event
/// carrying <c>{fromIndex, toIndex, fromParticipantId, toParticipantId}</c>
/// so the cross-session game-night timeline remains replayable.
/// </summary>
internal sealed class AdvanceTurnCommandHandler : ICommandHandler<AdvanceTurnCommand, AdvanceTurnCommandResult>
{
    private readonly ISessionRepository _sessionRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly MeepleAiDbContext _db;
    private readonly TimeProvider _timeProvider;

    public AdvanceTurnCommandHandler(
        ISessionRepository sessionRepository,
        IUnitOfWork unitOfWork,
        MeepleAiDbContext db,
        TimeProvider timeProvider)
    {
        _sessionRepository = sessionRepository ?? throw new ArgumentNullException(nameof(sessionRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    public async Task<AdvanceTurnCommandResult> Handle(AdvanceTurnCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var session = await _sessionRepository
            .GetByIdAsync(request.SessionId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException($"Session {request.SessionId} not found.");

        if (session.UserId != request.UserId)
        {
            throw new ForbiddenException("Only the session owner can advance the turn.");
        }

        // Domain validates: Status == Active and TurnOrder set. Throws ConflictException.
        var domainResult = session.AdvanceTurn();

        await _sessionRepository.UpdateAsync(session, cancellationToken).ConfigureAwait(false);

        // Resolve GameNightId via the link row (if any) so the diary entry is
        // correlated with the cross-game timeline.
        var gameNightId = await _db.ResolveGameNightIdAsync(session.Id, cancellationToken).ConfigureAwait(false);

        var payload = JsonSerializer.Serialize(new
        {
            fromIndex = domainResult.FromIndex,
            toIndex = domainResult.ToIndex,
            fromParticipantId = domainResult.FromParticipantId,
            toParticipantId = domainResult.ToParticipantId
        });

        _db.SessionEvents.Add(new SessionEventEntity
        {
            Id = Guid.NewGuid(),
            SessionId = session.Id,
            GameNightId = gameNightId,
            EventType = "turn_advanced",
            Timestamp = _timeProvider.GetUtcNow().UtcDateTime,
            Payload = payload,
            CreatedBy = request.UserId,
            Source = "user",
            IsDeleted = false
        });

        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return new AdvanceTurnCommandResult(
            domainResult.FromIndex,
            domainResult.ToIndex,
            domainResult.FromParticipantId,
            domainResult.ToParticipantId);
    }
}
