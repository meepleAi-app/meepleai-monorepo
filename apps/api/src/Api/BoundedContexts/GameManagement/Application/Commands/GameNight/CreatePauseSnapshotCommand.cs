using System.Text.Json;
using Api.BoundedContexts.GameManagement.Domain.Entities;
using Api.BoundedContexts.GameManagement.Domain.Entities.PauseSnapshot;
using Api.BoundedContexts.GameManagement.Domain.Enums;
using Api.BoundedContexts.GameManagement.Domain.Events;
using Api.BoundedContexts.GameManagement.Domain.Repositories;
using Api.Infrastructure;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Services;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.GameManagement.Application.Commands.GameNight;

/// <summary>
/// Command that captures the full state of an in-progress live game session
/// into a PauseSnapshot and transitions the session to Paused status.
/// Game Night Improvvisata — E4: Save/Resume flow.
/// </summary>
public sealed record CreatePauseSnapshotCommand(
    Guid SessionId,
    Guid SavedByUserId,
    List<Guid>? FinalPhotoIds = null) : IRequest<Guid>;

/// <summary>
/// Validates <see cref="CreatePauseSnapshotCommand"/> inputs.
/// </summary>
public sealed class CreatePauseSnapshotCommandValidator : AbstractValidator<CreatePauseSnapshotCommand>
{
    public CreatePauseSnapshotCommandValidator()
    {
        RuleFor(x => x.SessionId)
            .NotEmpty()
            .WithMessage("Session ID is required");

        RuleFor(x => x.SavedByUserId)
            .NotEmpty()
            .WithMessage("User ID is required");
    }
}

/// <summary>
/// Handles <see cref="CreatePauseSnapshotCommand"/>.
/// Captures session state, creates a PauseSnapshot, pauses the session,
/// then publishes <see cref="SessionPausedEvent"/> (for SignalR broadcast).
/// </summary>
internal sealed class CreatePauseSnapshotCommandHandler
    : IRequestHandler<CreatePauseSnapshotCommand, Guid>
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly ILiveSessionRepository _sessionRepository;
    private readonly IPauseSnapshotRepository _snapshotRepository;
    private readonly MeepleAiDbContext _dbContext;
    private readonly ITierEnforcementService _tierEnforcementService;
    private readonly IPublisher _publisher;
    private readonly ILogger<CreatePauseSnapshotCommandHandler> _logger;

    public CreatePauseSnapshotCommandHandler(
        ILiveSessionRepository sessionRepository,
        IPauseSnapshotRepository snapshotRepository,
        MeepleAiDbContext dbContext,
        ITierEnforcementService tierEnforcementService,
        IPublisher publisher,
        ILogger<CreatePauseSnapshotCommandHandler> logger)
    {
        _sessionRepository = sessionRepository ?? throw new ArgumentNullException(nameof(sessionRepository));
        _snapshotRepository = snapshotRepository ?? throw new ArgumentNullException(nameof(snapshotRepository));
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _tierEnforcementService = tierEnforcementService ?? throw new ArgumentNullException(nameof(tierEnforcementService));
        _publisher = publisher ?? throw new ArgumentNullException(nameof(publisher));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<Guid> Handle(
        CreatePauseSnapshotCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        // 1. Load LiveGameSession — throws NotFoundException if missing
        var session = await _sessionRepository
            .GetByIdAsync(request.SessionId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException("LiveGameSession", request.SessionId.ToString());

        // 2. Authorization: only the session host/creator can pause the session
        if (session.CreatedByUserId != request.SavedByUserId)
        {
            throw new ForbiddenException(
                "Only the session host can save and pause the session.");
        }

        // 3. Verify session is InProgress
        if (session.Status != LiveSessionStatus.InProgress)
        {
            throw new ConflictException(
                $"Cannot create a pause snapshot for session in {session.Status} status. " +
                "Session must be InProgress.");
        }

        // 4. Check tier: session save must be enabled for the user's plan
        var limits = await _tierEnforcementService
            .GetLimitsAsync(request.SavedByUserId, cancellationToken)
            .ConfigureAwait(false);

        if (!limits.SessionSaveEnabled)
        {
            throw new ConflictException(
                "Il salvataggio della sessione non è disponibile nel piano gratuito. Passa a Premium per salvare le partite.");
        }

        _logger.LogInformation(
            "CreatePauseSnapshotCommand: SessionId={SessionId}, SavedByUserId={UserId}",
            request.SessionId, request.SavedByUserId);

        // 5. Capture player scores from the live session
        var playerScores = session.Players
            .Where(p => p.IsActive)
            .Select(p => new PlayerScoreSnapshot(
                PlayerName: p.DisplayName,
                Score: p.TotalScore))
            .ToList();

        // 6. Capture current phase name
        var currentPhase = session.GetCurrentPhaseName();

        // 7. Create PauseSnapshot via factory (IsAutoSave = false)
        var snapshot = PauseSnapshot.Create(
            liveGameSessionId: session.Id,
            currentTurn: session.CurrentTurnIndex,
            currentPhase: currentPhase,
            playerScores: playerScores,
            savedByUserId: request.SavedByUserId,
            isAutoSave: false,
            attachmentIds: request.FinalPhotoIds,
            disputes: session.Disputes.ToList());

        // 6. Save snapshot via repository
        await _snapshotRepository.AddAsync(snapshot, cancellationToken).ConfigureAwait(false);

        // 7. Pause the session (domain method handles validation)
        session.Pause();

        // 8. Persist session status change to DB
        await PersistSessionStatusAsync(session, cancellationToken).ConfigureAwait(false);

        // 9. Update in-memory repository
        await _sessionRepository.UpdateAsync(session, cancellationToken).ConfigureAwait(false);

        // 10. Save snapshot to DB
        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        // 11. Publish SessionPausedEvent for SignalR broadcast
        // Note: SessionSaveRequestedEvent (AI summary) was gated on ChatSessionId (ADR-083 SP0 dead-code, removed).
        await _publisher
            .Publish(new SessionPausedEvent(session.Id), cancellationToken)
            .ConfigureAwait(false);

        _logger.LogInformation(
            "Session {SessionId} paused. Snapshot {SnapshotId} created with {PlayerCount} player scores.",
            session.Id, snapshot.Id, playerScores.Count);

        return snapshot.Id;
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

    private async Task PersistSessionStatusAsync(
        LiveGameSession session,
        CancellationToken cancellationToken)
    {
        var entity = await _dbContext.LiveGameSessions
            .FirstOrDefaultAsync(e => e.Id == session.Id, cancellationToken)
            .ConfigureAwait(false);

        if (entity is null)
        {
            _logger.LogWarning(
                "LiveGameSessionEntity not found in DB for SessionId={SessionId}. " +
                "Status will not be persisted to database.",
                session.Id);
            return;
        }

        entity.Status = (int)session.Status;
        entity.PausedAt = session.PausedAt;
        entity.UpdatedAt = session.UpdatedAt;
        entity.DisputesJson = JsonSerializer.Serialize(session.Disputes, JsonOptions);
    }

}
