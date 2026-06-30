using Api.BoundedContexts.GameManagement.Application.Commands.LiveSessions;
using Api.BoundedContexts.GameManagement.Domain.Entities;
using Api.BoundedContexts.GameManagement.Domain.Repositories;
using Api.BoundedContexts.GameManagement.Domain.Services;
using Api.BoundedContexts.GameManagement.Domain.ValueObjects;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Domain.Exceptions;
using Api.SharedKernel.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.GameManagement.Application.Commands.LiveSessions;

/// <summary>
/// Handles starting a live session.
/// Issue #4749: CQRS commands for live sessions.
/// Issue #2587 Slice 1 T2: On GameId-backed sessions, enforces quota and creates a correlated
/// GameSession aggregate so the session appears in history and consumes the user's quota slot.
/// </summary>
internal class StartLiveSessionCommandHandler : ICommandHandler<StartLiveSessionCommand>
{
    private readonly ILiveSessionRepository _sessionRepository;
    private readonly IGameSessionRepository _gameSessionRepository;
    private readonly ISessionQuotaService _quotaService;
    private readonly TimeProvider _timeProvider;
    private readonly IUnitOfWork _unitOfWork;

    public StartLiveSessionCommandHandler(
        ILiveSessionRepository sessionRepository,
        IGameSessionRepository gameSessionRepository,
        ISessionQuotaService quotaService,
        TimeProvider timeProvider,
        IUnitOfWork unitOfWork)
    {
        _sessionRepository = sessionRepository ?? throw new ArgumentNullException(nameof(sessionRepository));
        _gameSessionRepository = gameSessionRepository ?? throw new ArgumentNullException(nameof(gameSessionRepository));
        _quotaService = quotaService ?? throw new ArgumentNullException(nameof(quotaService));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    }

    public async Task Handle(StartLiveSessionCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var session = await _sessionRepository.GetByIdAsync(command.SessionId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException("LiveGameSession", command.SessionId.ToString());

        // Issue #2587 Slice 1: Create correlated GameSession on first start of a GameId-backed session.
        // Idempotent: CorrelatedGameSessionId != null means correlation already done (re-start guard).
        if (session.GameId.HasValue && session.CorrelatedGameSessionId == null)
        {
            var quotaResult = await _quotaService
                .CheckQuotaAsync(command.UserId, command.UserTier, command.UserRole, cancellationToken)
                .ConfigureAwait(false);

            if (!quotaResult.IsAllowed)
                throw new QuotaExceededException("SessionQuota", quotaResult.DenialReason ?? "Session limit reached");

            var players = session.Players
                .Where(p => p.IsActive)
                .Select((p, i) => new SessionPlayer(p.DisplayName, i + 1))
                .ToList();

            var gameSession = new GameSession(
                id: Guid.NewGuid(),
                gameId: session.GameId.Value,
                players: players,
                createdByUserId: session.CreatedByUserId);

            session.SetCorrelatedGameSessionId(gameSession.Id);

            await _gameSessionRepository.AddAsync(gameSession, cancellationToken).ConfigureAwait(false);
        }

        session.Start(_timeProvider);
        await _sessionRepository.UpdateAsync(session, cancellationToken).ConfigureAwait(false);

        try
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateConcurrencyException)
        {
            // Race: another concurrent caller already started this session.
            // Re-fetch to check whether they completed correlation.
            var refreshed = await _sessionRepository
                .GetByIdAsync(command.SessionId, cancellationToken)
                .ConfigureAwait(false);

            if (refreshed?.CorrelatedGameSessionId != null)
                return; // The winner already correlated — idempotent success.

            throw;
        }
    }
}

/// <summary>
/// Handles pausing a live session.
/// Issue #4749: CQRS commands for live sessions.
/// </summary>
internal class PauseLiveSessionCommandHandler : ICommandHandler<PauseLiveSessionCommand>
{
    private readonly ILiveSessionRepository _sessionRepository;
    private readonly TimeProvider _timeProvider;
    private readonly IUnitOfWork _unitOfWork;

    public PauseLiveSessionCommandHandler(
        ILiveSessionRepository sessionRepository,
        TimeProvider timeProvider,
        IUnitOfWork unitOfWork)
    {
        _sessionRepository = sessionRepository ?? throw new ArgumentNullException(nameof(sessionRepository));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    }

    public async Task Handle(PauseLiveSessionCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var session = await _sessionRepository.GetByIdAsync(command.SessionId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException("LiveGameSession", command.SessionId.ToString());

        session.Pause(_timeProvider);
        await _sessionRepository.UpdateAsync(session, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}

/// <summary>
/// Handles resuming a paused live session.
/// Issue #4749: CQRS commands for live sessions.
/// </summary>
internal class ResumeLiveSessionCommandHandler : ICommandHandler<ResumeLiveSessionCommand>
{
    private readonly ILiveSessionRepository _sessionRepository;
    private readonly TimeProvider _timeProvider;
    private readonly IUnitOfWork _unitOfWork;

    public ResumeLiveSessionCommandHandler(
        ILiveSessionRepository sessionRepository,
        TimeProvider timeProvider,
        IUnitOfWork unitOfWork)
    {
        _sessionRepository = sessionRepository ?? throw new ArgumentNullException(nameof(sessionRepository));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    }

    public async Task Handle(ResumeLiveSessionCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var session = await _sessionRepository.GetByIdAsync(command.SessionId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException("LiveGameSession", command.SessionId.ToString());

        session.Resume(_timeProvider);
        await _sessionRepository.UpdateAsync(session, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}

/// <summary>
/// Handles completing a live session.
/// Triggers PlayRecord generation via domain events.
/// Issue #4749: CQRS commands for live sessions.
/// Issue #2587 Slice 1 T3: Also completes the correlated GameSession (if any) so it stops
/// counting against the user's active quota. Guard against double-complete when the
/// GameSession is already in a terminal state (Completed/Abandoned).
/// </summary>
internal class CompleteLiveSessionCommandHandler : ICommandHandler<CompleteLiveSessionCommand>
{
    private readonly ILiveSessionRepository _sessionRepository;
    private readonly IGameSessionRepository _gameSessionRepository;
    private readonly TimeProvider _timeProvider;
    private readonly IUnitOfWork _unitOfWork;

    public CompleteLiveSessionCommandHandler(
        ILiveSessionRepository sessionRepository,
        IGameSessionRepository gameSessionRepository,
        TimeProvider timeProvider,
        IUnitOfWork unitOfWork)
    {
        _sessionRepository = sessionRepository ?? throw new ArgumentNullException(nameof(sessionRepository));
        _gameSessionRepository = gameSessionRepository ?? throw new ArgumentNullException(nameof(gameSessionRepository));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    }

    public async Task Handle(CompleteLiveSessionCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var session = await _sessionRepository.GetByIdAsync(command.SessionId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException("LiveGameSession", command.SessionId.ToString());

        session.Complete(_timeProvider);
        await _sessionRepository.UpdateAsync(session, cancellationToken).ConfigureAwait(false);

        // Issue #2587 Slice 1: Lifecycle-sync — complete the correlated GameSession so it
        // is no longer counted as active in the user's quota.
        // The correlated GameSession is created in Setup status at live-session start and is
        // never explicitly progressed, so we drive it through Start → Complete to honour the
        // GameSession state machine (Complete() guards InProgress/Paused).
        if (session.CorrelatedGameSessionId.HasValue)
        {
            var gameSession = await _gameSessionRepository
                .GetByIdAsync(session.CorrelatedGameSessionId.Value, cancellationToken)
                .ConfigureAwait(false);

            if (gameSession != null && !gameSession.Status.IsFinished)
            {
                // Drive from Setup → InProgress → Completed so the domain invariants are met.
                if (gameSession.Status == SessionStatus.Setup)
                    gameSession.Start();

                gameSession.Complete();
                await _gameSessionRepository.UpdateAsync(gameSession, cancellationToken).ConfigureAwait(false);
            }
            // If gameSession is null or already finished → idempotent, no action.
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}

/// <summary>
/// Handles saving the current session state.
/// Issue #4749: CQRS commands for live sessions.
/// </summary>
internal class SaveLiveSessionCommandHandler : ICommandHandler<SaveLiveSessionCommand>
{
    private readonly ILiveSessionRepository _sessionRepository;
    private readonly TimeProvider _timeProvider;
    private readonly IUnitOfWork _unitOfWork;

    public SaveLiveSessionCommandHandler(
        ILiveSessionRepository sessionRepository,
        TimeProvider timeProvider,
        IUnitOfWork unitOfWork)
    {
        _sessionRepository = sessionRepository ?? throw new ArgumentNullException(nameof(sessionRepository));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    }

    public async Task Handle(SaveLiveSessionCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var session = await _sessionRepository.GetByIdAsync(command.SessionId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException("LiveGameSession", command.SessionId.ToString());

        session.Save(_timeProvider);
        await _sessionRepository.UpdateAsync(session, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
