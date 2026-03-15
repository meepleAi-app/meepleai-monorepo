using Api.BoundedContexts.GameManagement.Domain.Entities;
using Api.BoundedContexts.GameManagement.Domain.Enums;
using Api.BoundedContexts.GameManagement.Domain.Events;
using Api.BoundedContexts.GameManagement.Domain.Repositories;
using Api.Infrastructure;
using Api.Infrastructure.Entities.GameManagement;
using Api.Middleware.Exceptions;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.GameManagement.Application.Commands.GameNight;

/// <summary>
/// Command to resume a paused live game session from its most recent PauseSnapshot.
/// Creates a new SessionInvite (old one may be expired), deletes stale auto-saves,
/// and returns an optional agent recap for context.
/// Game Night Improvvisata — E4: Save/Resume flow.
/// </summary>
public sealed record ResumeSessionFromSnapshotCommand(
    Guid SessionId,
    Guid ResumedByUserId) : IRequest<ResumeSessionResponse>;

/// <summary>
/// Response returned after a successful session resume.
/// </summary>
public sealed record ResumeSessionResponse(
    Guid SessionId,
    string InviteCode,
    string ShareLink,
    string? AgentRecap);

/// <summary>
/// Validates <see cref="ResumeSessionFromSnapshotCommand"/> inputs.
/// </summary>
public sealed class ResumeSessionFromSnapshotCommandValidator
    : AbstractValidator<ResumeSessionFromSnapshotCommand>
{
    public ResumeSessionFromSnapshotCommandValidator()
    {
        RuleFor(x => x.SessionId)
            .NotEmpty()
            .WithMessage("Session ID is required");

        RuleFor(x => x.ResumedByUserId)
            .NotEmpty()
            .WithMessage("User ID is required");
    }
}

/// <summary>
/// Handles <see cref="ResumeSessionFromSnapshotCommand"/>.
/// Orchestrates the resume flow:
///   1. Load latest PauseSnapshot.
///   2. Load LiveGameSession and verify it's Paused.
///   3. Resume session via domain method.
///   4. Issue a fresh SessionInvite.
///   5. Delete auto-save PauseSnapshots.
///   6. Build recap from agent summary (or fallback).
///   7. Publish <see cref="SessionResumedEvent"/>.
/// </summary>
internal sealed class ResumeSessionFromSnapshotCommandHandler
    : IRequestHandler<ResumeSessionFromSnapshotCommand, ResumeSessionResponse>
{
    private const int InviteMaxUses = 10;
    private const int InviteExpiryMinutes = 24 * 60; // 24 hours

    private readonly ILiveSessionRepository _sessionRepository;
    private readonly IPauseSnapshotRepository _snapshotRepository;
    private readonly MeepleAiDbContext _dbContext;
    private readonly IPublisher _publisher;
    private readonly ILogger<ResumeSessionFromSnapshotCommandHandler> _logger;

    public ResumeSessionFromSnapshotCommandHandler(
        ILiveSessionRepository sessionRepository,
        IPauseSnapshotRepository snapshotRepository,
        MeepleAiDbContext dbContext,
        IPublisher publisher,
        ILogger<ResumeSessionFromSnapshotCommandHandler> logger)
    {
        _sessionRepository = sessionRepository ?? throw new ArgumentNullException(nameof(sessionRepository));
        _snapshotRepository = snapshotRepository ?? throw new ArgumentNullException(nameof(snapshotRepository));
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _publisher = publisher ?? throw new ArgumentNullException(nameof(publisher));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<ResumeSessionResponse> Handle(
        ResumeSessionFromSnapshotCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        // 1. Load latest PauseSnapshot — throws NotFoundException if none exists
        var snapshot = await _snapshotRepository
            .GetLatestBySessionIdAsync(request.SessionId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException("PauseSnapshot", request.SessionId.ToString());

        // 2. Load LiveGameSession
        var session = await _sessionRepository
            .GetByIdAsync(request.SessionId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException("LiveGameSession", request.SessionId.ToString());

        // 3. Verify session is Paused
        if (session.Status != LiveSessionStatus.Paused)
        {
            throw new ConflictException(
                $"Cannot resume session in {session.Status} status. Session must be Paused.");
        }

        _logger.LogInformation(
            "ResumeSessionFromSnapshotCommand: SessionId={SessionId}, SnapshotId={SnapshotId}",
            request.SessionId, snapshot.Id);

        // 4. Resume the session via domain method
        session.Resume();

        // 5. Create a new SessionInvite (old one may have expired)
        var invite = SessionInvite.Create(
            sessionId: session.Id,
            createdByUserId: request.ResumedByUserId,
            maxUses: InviteMaxUses,
            expiryMinutes: InviteExpiryMinutes);

        // 6. Delete auto-save PauseSnapshots to keep history clean
        await _snapshotRepository
            .DeleteAutoSavesBySessionIdAsync(request.SessionId, cancellationToken)
            .ConfigureAwait(false);

        // 7. Get agent recap from the snapshot's conversation summary (may be null — that's OK)
        var recap = BuildRecap(snapshot.AgentConversationSummary, snapshot.CurrentTurn);

        // 8. Persist session status change to DB
        await PersistSessionStatusAsync(session, cancellationToken).ConfigureAwait(false);

        // 9. Persist new SessionInvite to DB
        _dbContext.SessionInvites.Add(new SessionInviteEntity
        {
            Id = invite.Id,
            SessionId = invite.SessionId,
            CreatedByUserId = invite.CreatedByUserId,
            Pin = invite.Pin,
            LinkToken = invite.LinkToken,
            MaxUses = invite.MaxUses,
            CurrentUses = invite.CurrentUses,
            CreatedAt = invite.CreatedAt,
            ExpiresAt = invite.ExpiresAt,
            IsRevoked = invite.IsRevoked
        });

        // 10. Update in-memory repository
        await _sessionRepository.UpdateAsync(session, cancellationToken).ConfigureAwait(false);

        // 11. Save everything atomically
        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        // 12. Publish SessionResumedEvent for SignalR broadcast
        await _publisher
            .Publish(new SessionResumedEvent(session.Id, recap), cancellationToken)
            .ConfigureAwait(false);

        _logger.LogInformation(
            "Session {SessionId} resumed. New invite {InvitePin} created. Recap available: {HasRecap}",
            session.Id, invite.Pin, recap is not null);

        var shareLink = $"/join/{invite.LinkToken}";

        return new ResumeSessionResponse(
            SessionId: session.Id,
            InviteCode: invite.Pin,
            ShareLink: shareLink,
            AgentRecap: recap);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static string BuildRecap(string? agentSummary, int currentTurn)
    {
        if (!string.IsNullOrWhiteSpace(agentSummary))
            return agentSummary;

        // Fallback when no AI summary is available
        return $"Sessione ripresa dal turno {currentTurn}.";
    }

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
    }
}
