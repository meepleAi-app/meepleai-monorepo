using Api.BoundedContexts.GameManagement.Domain.Entities.GameNightEvent;
using Api.BoundedContexts.GameManagement.Domain.Enums;
using Api.BoundedContexts.GameManagement.Domain.Exceptions;
using Api.BoundedContexts.SessionTracking.Application.Commands;
using Api.Infrastructure.EntityConfigurations.GameManagement;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.GameManagement.Application.Commands.GameNights;

/// <summary>
/// Handles <see cref="GoLiveSessionCommand"/> — the explicit go-live sub-resource (epic #3188
/// Slice 2, made idempotent / self-healing in #3218). Mirrors phases 2+3 of
/// <see cref="StartGameNightSessionCommandHandler"/>, minus the cross-BC session create (the draft
/// already exists).
///
/// <para><b>#3218 self-healing:</b> the go-live is a multi-phase, multi-SaveChanges flow — phase 2
/// (the link promotion <c>Pending → InProgress</c>) commits in its own transaction, then phase 3
/// (<see cref="OpenSessionLiveModeCommand"/>) commits the tracking-Session live-mode transition in a
/// second one. If phase 3 fails after phase 2 committed, the link is stuck <c>InProgress</c> while
/// the tracking Session is not yet live — a split-brain. To let the natural retry affordance heal it,
/// the handler branches on the TARGET link's current status AFTER authorization:</para>
///
/// <list type="bullet">
///   <item><b>link <c>Pending</c></b> — promote (<c>Pending → InProgress</c>) via
///     <see cref="GameNightEvent.StartSession"/> + persist (the aggregate root's xmin write detects a
///     concurrent live-slot race), THEN dispatch <see cref="OpenSessionLiveModeCommand"/>.</item>
///   <item><b>link <c>InProgress</c></b> (a prior attempt already committed phase 2) — SELF-HEAL:
///     SKIP the promotion + the aggregate write entirely and go straight to dispatching
///     <see cref="OpenSessionLiveModeCommand"/>. That handler is idempotent (<c>if (session.IsLive)
///     return;</c>), so this brings a not-yet-live Session live and is a harmless no-op if it is
///     already live. Returns the current promoted state — no 409 on retry.</item>
///   <item><b>link terminal</b> (<c>Completed</c>/<c>Skipped</c>/<c>Corrupted</c>) —
///     <see cref="ConflictException"/> → 409: a finished session cannot go live.</item>
/// </list>
///
/// <para>Phase 3 is dispatched LAST — AFTER the promotion is committed (or skipped) — so
/// <c>Session.OpenLiveMode()</c>'s <c>SessionStartedDomainEvent</c> resolves the parent unambiguously
/// and drives the invariante #15 night promotion (Published → InProgress).</para>
///
/// <para>Error mapping (never 500):
/// <list type="bullet">
///   <item>Owning night / target link not found → <see cref="NotFoundException"/> → 404.</item>
///   <item>Caller is not the organizer → <see cref="ForbiddenException"/> → 403 (checked AFTER the
///     404 resolution so a bad id never leaks existence via a 403).</item>
///   <item>Terminal target link → <see cref="ConflictException"/> → 409.</item>
///   <item>Another session already live (in-memory guard, Pending path only) →
///     <see cref="MaxLiveSessionsExceededException"/> → 409.</item>
///   <item>Concurrent go-live loses the xmin race (Pending path only) →
///     <see cref="DbUpdateConcurrencyException"/> re-mapped to
///     <see cref="MaxLiveSessionsExceededException"/> → 409.</item>
///   <item>Concurrent promotion trips the partial-unique live-slot index (Postgres 23505, Pending
///     path only) → re-mapped to <see cref="MaxLiveSessionsExceededException"/> → 409.</item>
/// </list></para>
///
/// <para>The self-heal path does NO aggregate write, so it needs no concurrency catch.</para>
/// </summary>
internal sealed class GoLiveSessionCommandHandler : ICommandHandler<GoLiveSessionCommand, GoLiveSessionResult>
{
    private readonly IGameNightEventRepository _repository;
    private readonly IMediator _mediator;
    private readonly IUnitOfWork _unitOfWork;

    public GoLiveSessionCommandHandler(
        IGameNightEventRepository repository,
        IMediator mediator,
        IUnitOfWork unitOfWork)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    }

    public async Task<GoLiveSessionResult> Handle(GoLiveSessionCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        // Detached load (AsNoTracking) so the detached UpdateAsync full-remap + .Update() below
        // does not collide with a tracked instance on the same key (mirrors GetByIdAsync).
        var gameNight = await _repository
            .GetByLinkedSessionIdAsync(command.SessionId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException("GameNightSession", command.SessionId.ToString());

        // Authorization: only the night's organizer may take a session live (parity with
        // StartGameNightSessionCommandHandler). Checked AFTER the not-found resolution so a bad id
        // still returns 404, not a 403 that would leak the existence of the resource.
        if (gameNight.OrganizerId != command.UserId)
            throw new ForbiddenException("Only the organizer can start sessions.");

        // Resolve the TARGET link and branch on its current status for idempotent / self-healing
        // go-live (#3218). GetByLinkedSessionIdAsync returns the OWNING night, so the link should
        // exist — but guard the lookup so a defensive miss maps to 404, never an unhandled 500.
        var targetLink = gameNight.Sessions.FirstOrDefault(s => s.SessionId == command.SessionId)
            ?? throw new NotFoundException("GameNightSession", command.SessionId.ToString());

        switch (targetLink.Status)
        {
            case GameNightSessionStatus.Pending:
                // Normal path — promote (Pending → InProgress) + persist, with the xmin/23505 → 409
                // and non-Pending → 409 catches scoped to THIS aggregate write.
                await PromotePendingDraftAsync(gameNight, command.SessionId, cancellationToken).ConfigureAwait(false);
                break;

            case GameNightSessionStatus.InProgress:
                // SELF-HEAL (#3218) — a prior go-live attempt already committed the promotion but did
                // not finish opening live mode (a phase-3 failure split-brain), OR the whole flow
                // already succeeded and this is a plain retry. Either way SKIP the promotion and the
                // aggregate write, and fall through to dispatching OpenSessionLiveModeCommand below,
                // which is idempotent: it brings a not-yet-live Session live and no-ops if already live.
                break;

            default:
                // Terminal link (Completed / Skipped / Corrupted): a finished session cannot go live.
                throw new ConflictException(
                    $"Cannot go live: session {command.SessionId} is {targetLink.Status} and cannot be started.");
        }

        var promoted = gameNight.Sessions.First(s => s.SessionId == command.SessionId);
        var result = new GoLiveSessionResult(
            command.SessionId,
            gameNight.Id,
            promoted.Id,
            promoted.PlayOrder,
            promoted.Status.ToString());

        // Phase 3: open live mode LAST — AFTER the session↔night promotion is committed (Pending path)
        // or skipped (self-heal path) — so the SessionStartedDomainEvent resolves the parent
        // unambiguously and promotes the night Published → InProgress (invariante #15). Idempotent.
        await _mediator.Send(new OpenSessionLiveModeCommand(command.SessionId), cancellationToken).ConfigureAwait(false);

        return result;
    }

    /// <summary>
    /// Pending-path phase 2: promote the targeted draft (Pending → InProgress) and persist, mapping
    /// the aggregate write's failure modes to 409 (never a 500). Only ever called for a link the
    /// caller has already confirmed is <see cref="GameNightSessionStatus.Pending"/>.
    /// </summary>
    private async Task PromotePendingDraftAsync(
        GameNightEvent gameNight, Guid sessionId, CancellationToken cancellationToken)
    {
        try
        {
            // Promote the targeted draft (Pending → InProgress). MaxLive (another session already
            // live) escapes this try uncaught (it already maps to 409); a corrupted-aggregate guard
            // throws InvalidOperationException which the catch below normalises to a 409.
            gameNight.StartSession(sessionId);

            await _repository.UpdateAsync(gameNight, cancellationToken).ConfigureAwait(false);
            // The aggregate root's xmin write detects a concurrent go-live that already took the
            // night's single live slot (0 rows updated → DbUpdateConcurrencyException).
            await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateConcurrencyException)
        {
            // xmin loser: a sibling go-live promoted first — map to the same blocked 409 as the
            // in-memory guard instead of an uncaught 500.
            throw new MaxLiveSessionsExceededException(gameNight.Id);
        }
        catch (DbUpdateException ex) when (IsGameNightLiveSlotViolation(ex))
        {
            // Belt-and-suspenders: the partial-unique live-slot index rejected a second InProgress
            // row for this night. Dedicated catch on THIS write — deliberately not relying on the
            // create-path catch in CreateSessionCommandHandler.
            throw new MaxLiveSessionsExceededException(gameNight.Id);
        }
        catch (InvalidOperationException ex)
        {
            // Corrupted-aggregate guard (or any other domain conflict on the promotion) — a conflict,
            // not a 500.
            throw new ConflictException(ex.Message);
        }
    }

    // True iff the DbUpdateException is the partial-unique-index violation on the GameNight live slot
    // (game_night_sessions InProgress per event), not some other constraint. Uses the shared index-name
    // constant so this catch can never drift from the index declaration.
    private static bool IsGameNightLiveSlotViolation(DbUpdateException ex) =>
        ex.InnerException is Npgsql.PostgresException pg
        && string.Equals(pg.SqlState, Npgsql.PostgresErrorCodes.UniqueViolation, StringComparison.Ordinal)
        && string.Equals(pg.ConstraintName, GameNightSessionEntityConfiguration.UniqueActiveIndexName, StringComparison.Ordinal);
}
