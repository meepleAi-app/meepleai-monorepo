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

        // Issue #2608: Only the session creator may start a GameId-backed session.
        // The correlated GameSession is attributed to CreatedByUserId and the quota is checked
        // against that user — allowing any participant to start would decouple the quota gate
        // from the quota owner (non-creator starts → B's quota gates but A's slot is consumed).
        // .RequireLiveSessionParticipant() on the endpoint remains as defense-in-depth (rejects
        // non-participants before this point); this guard adds the creator-only business rule.
        if (command.UserId != session.CreatedByUserId)
            throw new ForbiddenException("Only the session creator can start the session.");

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

            // Issue #2608: Guard against starting with zero active players so the caller
            // receives a clear, intentional error rather than a generic GameSession ctor failure.
            if (players.Count == 0)
                throw new ValidationException("Cannot start a session with no active players.");

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
        // Use MarkCorrelatedComplete (event-free bookkeeping) rather than Start()+Complete()
        // so that no spurious GameSessionStartedEvent / GameSessionCompletedEvent is raised.
        // The real session lifecycle side-effects are owned by LiveGameSession and its own
        // domain events; firing them here would generate wrong audit entries and unintentionally
        // credit SharedGameCatalog contributors (SessionCompletedForContributorsHandler).
        if (session.CorrelatedGameSessionId.HasValue)
        {
            var gameSession = await _gameSessionRepository
                .GetByIdAsync(session.CorrelatedGameSessionId.Value, cancellationToken)
                .ConfigureAwait(false);

            if (gameSession != null && !gameSession.Status.IsFinished)
            {
                gameSession.MarkCorrelatedComplete(_timeProvider);
                await _gameSessionRepository.UpdateAsync(gameSession, cancellationToken).ConfigureAwait(false);
            }
            // If gameSession is null or already finished → idempotent, no action.
        }
        else if (session.GameId.HasValue)
        {
            // Issue #2587 Slice 2: Backfill for legacy sessions started before Slice 1 shipped.
            // CorrelatedGameSessionId == null && GameId != null means a GameId-backed session
            // that never went through the Slice 1 correlation saga (started before it).
            // Create the correlated GameSession already in Completed state so it appears in
            // the user's session history. No quota check: completing ≠ starting, a Completed
            // GameSession does not consume an active quota slot.
            var players = session.Players
                .Where(p => p.IsActive)
                .Select((p, i) => new SessionPlayer(p.DisplayName, i + 1))
                .ToList();

            // Guard: GameSession ctor requires ≥1 player. A legacy session with no active
            // players still completes — it just doesn't produce a history record.
            if (players.Count > 0)
            {
                var gameSession = new GameSession(
                    id: Guid.NewGuid(),
                    gameId: session.GameId.Value,
                    players: players,
                    createdByUserId: session.CreatedByUserId);

                gameSession.MarkCorrelatedComplete(_timeProvider);
                session.SetCorrelatedGameSessionId(gameSession.Id);

                await _gameSessionRepository.AddAsync(gameSession, cancellationToken).ConfigureAwait(false);
            }
        }
        // else: free-form session (GameId == null, no GameId-backed history) → no GameSession created.

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
