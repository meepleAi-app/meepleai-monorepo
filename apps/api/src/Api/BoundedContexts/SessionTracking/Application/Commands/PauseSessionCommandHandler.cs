using Api.BoundedContexts.GameManagement.Domain.Enums;
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
/// domain <see cref="Session.Pause"/> transition, persists, and writes a
/// <c>session_paused</c> diary entry. The diary event is correlated with the
/// parent GameNight (if the session belongs to one) so it can be replayed in
/// the cross-game timeline.
/// </summary>
internal sealed class PauseSessionCommandHandler : ICommandHandler<PauseSessionCommand>
{
    private readonly ISessionRepository _sessionRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly MeepleAiDbContext _db;
    private readonly TimeProvider _timeProvider;

    public PauseSessionCommandHandler(
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

    public async Task Handle(PauseSessionCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var session = await _sessionRepository
            .GetByIdAsync(request.SessionId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException($"Session {request.SessionId} not found.");

        if (session.UserId != request.UserId)
        {
            throw new ForbiddenException("Only the session owner can pause this session.");
        }

        // Domain rule: throws ConflictException when not Active.
        session.Pause();

        await _sessionRepository.UpdateAsync(session, cancellationToken).ConfigureAwait(false);

        // Free the partial unique index slot on game_night_sessions: the link row that
        // currently advertises this session as InProgress must transition to Pending so
        // another session in the same night can become Active. (See T1 migration.)
        var linkRow = await _db.GameNightSessions
            .FirstOrDefaultAsync(gns => gns.SessionId == session.Id, cancellationToken)
            .ConfigureAwait(false);

        Guid? gameNightId = null;
        if (linkRow is not null)
        {
            gameNightId = linkRow.GameNightEventId;
            if (string.Equals(linkRow.Status, GameNightSessionStatus.InProgress.ToString(), StringComparison.Ordinal))
            {
                linkRow.Status = GameNightSessionStatus.Pending.ToString();
            }
        }

        _db.SessionEvents.Add(new SessionEventEntity
        {
            Id = Guid.NewGuid(),
            SessionId = session.Id,
            GameNightId = gameNightId,
            EventType = "session_paused",
            Timestamp = _timeProvider.GetUtcNow().UtcDateTime,
            Payload = "{}",
            CreatedBy = request.UserId,
            Source = "system",
            IsDeleted = false
        });

        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
