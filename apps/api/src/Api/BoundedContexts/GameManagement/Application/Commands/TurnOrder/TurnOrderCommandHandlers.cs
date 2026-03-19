using Api.BoundedContexts.GameManagement.Application.Commands.TurnOrder;
using Api.BoundedContexts.GameManagement.Application.DTOs.TurnOrder;
using Api.BoundedContexts.GameManagement.Application.Events;
using Api.BoundedContexts.GameManagement.Domain.Entities.TurnOrder;
using Api.BoundedContexts.GameManagement.Domain.Repositories;
using Api.BoundedContexts.SessionTracking.Domain.Services;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;

namespace Api.BoundedContexts.GameManagement.Application.Commands.TurnOrder;

/// <summary>
/// Initializes a new TurnOrder for a session.
/// Fails with ConflictException if one already exists.
/// Issue #4970: TurnOrder Entity + Endpoints + SSE.
/// </summary>
internal class InitializeTurnOrderCommandHandler
    : ICommandHandler<InitializeTurnOrderCommand, TurnOrderDto>
{
    private readonly ITurnOrderRepository _turnOrderRepository;
    private readonly ILiveSessionRepository _sessionRepository;
    private readonly IUnitOfWork _unitOfWork;

    public InitializeTurnOrderCommandHandler(
        ITurnOrderRepository turnOrderRepository,
        ILiveSessionRepository sessionRepository,
        IUnitOfWork unitOfWork)
    {
        _turnOrderRepository = turnOrderRepository ?? throw new ArgumentNullException(nameof(turnOrderRepository));
        _sessionRepository = sessionRepository ?? throw new ArgumentNullException(nameof(sessionRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    }

    public async Task<TurnOrderDto> Handle(InitializeTurnOrderCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        _ = await _sessionRepository.GetByIdAsync(command.SessionId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException("LiveGameSession", command.SessionId.ToString());

        var existing = await _turnOrderRepository.GetBySessionIdAsync(command.SessionId, cancellationToken)
            .ConfigureAwait(false);

        if (existing != null)
            throw new ConflictException("TurnOrder already initialized for this session.");

        var turnOrder = new Domain.Entities.TurnOrder.TurnOrder(
            Guid.NewGuid(),
            command.SessionId,
            command.PlayerOrder);

        await _turnOrderRepository.AddAsync(turnOrder, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return TurnOrderMapper.ToDto(turnOrder);
    }
}

/// <summary>
/// Advances the turn to the next player, broadcasting the SSE event.
/// Issue #4970: TurnOrder Entity + Endpoints + SSE.
/// </summary>
internal class AdvanceTurnCommandHandler : ICommandHandler<AdvanceTurnCommand, TurnOrderDto>
{
    private readonly ITurnOrderRepository _turnOrderRepository;
    private readonly ISessionBroadcastService _broadcastService;
    private readonly IUnitOfWork _unitOfWork;

    public AdvanceTurnCommandHandler(
        ITurnOrderRepository turnOrderRepository,
        ISessionBroadcastService broadcastService,
        IUnitOfWork unitOfWork)
    {
        _turnOrderRepository = turnOrderRepository ?? throw new ArgumentNullException(nameof(turnOrderRepository));
        _broadcastService = broadcastService ?? throw new ArgumentNullException(nameof(broadcastService));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    }

    public async Task<TurnOrderDto> Handle(AdvanceTurnCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var turnOrder = await _turnOrderRepository.GetBySessionIdAsync(command.SessionId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException("TurnOrder", command.SessionId.ToString());

        var previousPlayer = turnOrder.CurrentPlayer;

        turnOrder.Advance();

        await _turnOrderRepository.UpdateAsync(turnOrder, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        // Broadcast SSE event to all session participants
        var evt = new TurnAdvancedEvent(
            currentPlayerName: turnOrder.CurrentPlayer,
            previousPlayerName: previousPlayer,
            nextPlayerName: turnOrder.NextPlayer,
            roundNumber: turnOrder.RoundNumber);

        await _broadcastService.PublishAsync(
            command.SessionId,
            evt,
            EventVisibility.Public,
            cancellationToken).ConfigureAwait(false);

        return TurnOrderMapper.ToDto(turnOrder);
    }
}

/// <summary>
/// Replaces the player order with a new ordered list.
/// Issue #4970: TurnOrder Entity + Endpoints + SSE.
/// </summary>
internal class ReorderPlayersCommandHandler : ICommandHandler<ReorderPlayersCommand, TurnOrderDto>
{
    private readonly ITurnOrderRepository _turnOrderRepository;
    private readonly IUnitOfWork _unitOfWork;

    public ReorderPlayersCommandHandler(
        ITurnOrderRepository turnOrderRepository,
        IUnitOfWork unitOfWork)
    {
        _turnOrderRepository = turnOrderRepository ?? throw new ArgumentNullException(nameof(turnOrderRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    }

    public async Task<TurnOrderDto> Handle(ReorderPlayersCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var turnOrder = await _turnOrderRepository.GetBySessionIdAsync(command.SessionId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException("TurnOrder", command.SessionId.ToString());

        turnOrder.Reorder(command.NewPlayerOrder);

        await _turnOrderRepository.UpdateAsync(turnOrder, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return TurnOrderMapper.ToDto(turnOrder);
    }
}

/// <summary>
/// Resets the turn order back to round 1, first player.
/// Issue #4970: TurnOrder Entity + Endpoints + SSE.
/// </summary>
internal class ResetTurnOrderCommandHandler : ICommandHandler<ResetTurnOrderCommand, TurnOrderDto>
{
    private readonly ITurnOrderRepository _turnOrderRepository;
    private readonly IUnitOfWork _unitOfWork;

    public ResetTurnOrderCommandHandler(
        ITurnOrderRepository turnOrderRepository,
        IUnitOfWork unitOfWork)
    {
        _turnOrderRepository = turnOrderRepository ?? throw new ArgumentNullException(nameof(turnOrderRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    }

    public async Task<TurnOrderDto> Handle(ResetTurnOrderCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var turnOrder = await _turnOrderRepository.GetBySessionIdAsync(command.SessionId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException("TurnOrder", command.SessionId.ToString());

        turnOrder.Reset();

        await _turnOrderRepository.UpdateAsync(turnOrder, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return TurnOrderMapper.ToDto(turnOrder);
    }
}
