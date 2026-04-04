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
/// Handler for RollDiceCommand.
/// Creates a dice roll, persists it, and broadcasts via SSE.
/// </summary>
public class RollDiceCommandHandler : IRequestHandler<RollDiceCommand, RollDiceResult>
{
    private readonly ISessionRepository _sessionRepository;
    private readonly IDiceRollRepository _diceRollRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ISessionSyncService _syncService;

    public RollDiceCommandHandler(
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

    public async Task<RollDiceResult> Handle(RollDiceCommand request, CancellationToken cancellationToken)
    {
        // Verify session exists and is active
        var session = await _sessionRepository.GetByIdAsync(request.SessionId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException($"Session {request.SessionId} not found");

        if (session.Status != SessionStatus.Active)
        {
            throw new ConflictException($"Cannot roll dice in session with status {session.Status}");
        }

        // Verify participant belongs to session
        var participant = session.Participants.FirstOrDefault(p => p.Id == request.ParticipantId)
            ?? throw new NotFoundException($"Participant {request.ParticipantId} not found in session");

        // Create dice roll (domain logic handles formula parsing and rolling)
        var diceRoll = DiceRoll.Create(
            request.SessionId,
            request.ParticipantId,
            request.Formula,
            request.Label
        );

        // Persist dice roll
        await _diceRollRepository.AddAsync(diceRoll, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        // Publish SSE event for real-time updates
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

        return new RollDiceResult(
            diceRoll.Id,
            diceRoll.Formula,
            diceRoll.GetRolls(),
            diceRoll.Modifier,
            diceRoll.Total,
            diceRoll.Timestamp
        );
    }
}
