using System.Text.Json;
using MediatR;
using Api.BoundedContexts.SessionTracking.Application.Commands;
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

/// <summary>
/// Handles a role-validated dice roll player action.
/// Delegates to the existing DiceRoll domain logic, appends a
/// <c>dice_rolled</c> diary entry (Session Flow v2.1 — T7), and broadcasts a
/// <see cref="DiceRolledEvent"/>.
/// Issue #4765 - Player Action Endpoints
/// </summary>
public class RollSessionDiceCommandHandler : IRequestHandler<RollSessionDiceCommand, RollSessionDiceResult>
{
    private readonly ISessionRepository _sessionRepository;
    private readonly IDiceRollRepository _diceRollRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ISessionSyncService _syncService;
    private readonly MeepleAiDbContext _db;
    private readonly TimeProvider _timeProvider;

    public RollSessionDiceCommandHandler(
        ISessionRepository sessionRepository,
        IDiceRollRepository diceRollRepository,
        IUnitOfWork unitOfWork,
        ISessionSyncService syncService,
        MeepleAiDbContext db,
        TimeProvider timeProvider)
    {
        _sessionRepository = sessionRepository ?? throw new ArgumentNullException(nameof(sessionRepository));
        _diceRollRepository = diceRollRepository ?? throw new ArgumentNullException(nameof(diceRollRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _syncService = syncService ?? throw new ArgumentNullException(nameof(syncService));
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    public async Task<RollSessionDiceResult> Handle(RollSessionDiceCommand request, CancellationToken cancellationToken)
    {
        var session = await _sessionRepository.GetByIdAsync(request.SessionId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException($"Session {request.SessionId} not found");

        if (session.Status != SessionStatus.Active)
            throw new ConflictException($"Cannot roll dice in session with status {session.Status}");

        var participant = session.Participants.FirstOrDefault(p => p.Id == request.ParticipantId)
            ?? throw new NotFoundException($"Participant {request.ParticipantId} not found in session");

        var diceRoll = DiceRoll.Create(
            request.SessionId,
            request.ParticipantId,
            request.Formula,
            request.Label);

        await _diceRollRepository.AddAsync(diceRoll, cancellationToken).ConfigureAwait(false);

        // Session Flow v2.1 — T7: append a dice_rolled diary entry alongside the
        // DiceRoll persistence so the cross-game GameNight timeline reflects the
        // action. Resolve GameNightId via the link row (if any).
        var gameNightId = await _db.ResolveGameNightIdAsync(request.SessionId, cancellationToken).ConfigureAwait(false);

        var diaryPayload = JsonSerializer.Serialize(new
        {
            diceRollId = diceRoll.Id,
            formula = diceRoll.Formula,
            rolls = diceRoll.GetRolls(),
            modifier = diceRoll.Modifier,
            total = diceRoll.Total,
            label = diceRoll.Label,
            participantId = request.ParticipantId
        });

        _db.SessionEvents.Add(new SessionEventEntity
        {
            Id = Guid.NewGuid(),
            SessionId = request.SessionId,
            GameNightId = gameNightId,
            EventType = "dice_rolled",
            Timestamp = _timeProvider.GetUtcNow().UtcDateTime,
            Payload = diaryPayload,
            CreatedBy = request.RequesterId,
            Source = "user",
            IsDeleted = false
        });

        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var evt = new DiceRolledEvent
        {
            DiceRollId = diceRoll.Id,
            SessionId = request.SessionId,
            ParticipantId = request.ParticipantId,
            ParticipantName = participant.DisplayName,
            Formula = diceRoll.Formula,
            Label = diceRoll.Label,
            Rolls = diceRoll.GetRolls(),
            Modifier = diceRoll.Modifier,
            Total = diceRoll.Total,
            Timestamp = diceRoll.Timestamp
        };
        await _syncService.PublishEventAsync(request.SessionId, evt, cancellationToken).ConfigureAwait(false);

        return new RollSessionDiceResult(
            diceRoll.Id,
            diceRoll.Formula,
            diceRoll.GetRolls(),
            diceRoll.Modifier,
            diceRoll.Total,
            diceRoll.Timestamp);
    }
}
