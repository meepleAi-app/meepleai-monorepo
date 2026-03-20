using MediatR;
using Api.BoundedContexts.SessionTracking.Application.Commands;
using Api.BoundedContexts.SessionTracking.Domain.Entities;
using Api.BoundedContexts.SessionTracking.Domain.Events;
using Api.BoundedContexts.SessionTracking.Domain.Repositories;
using Api.BoundedContexts.SessionTracking.Domain.Services;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Infrastructure.Persistence;

namespace Api.BoundedContexts.SessionTracking.Application.Commands;

/// <summary>
/// Handles a role-validated dice roll player action.
/// Delegates to the existing DiceRoll domain logic and broadcasts a DiceRolledEvent.
/// Issue #4765 - Player Action Endpoints
/// </summary>
public class RollSessionDiceCommandHandler : IRequestHandler<RollSessionDiceCommand, RollSessionDiceResult>
{
    private readonly ISessionRepository _sessionRepository;
    private readonly IDiceRollRepository _diceRollRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ISessionSyncService _syncService;

    public RollSessionDiceCommandHandler(
        ISessionRepository sessionRepository,
        IDiceRollRepository diceRollRepository,
        IUnitOfWork unitOfWork,
        ISessionSyncService syncService)
    {
        _sessionRepository = sessionRepository ?? throw new ArgumentNullException(nameof(sessionRepository));
        _diceRollRepository = diceRollRepository ?? throw new ArgumentNullException(nameof(diceRollRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _syncService = syncService ?? throw new ArgumentNullException(nameof(syncService));
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
