using Api.BoundedContexts.GameManagement.Domain.Entities.GameNightEvent;
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
/// Slice 2). Mirrors phases 2+3 of <see cref="StartGameNightSessionCommandHandler"/>, minus the
/// cross-BC session create (the draft already exists):
///
/// <list type="number">
///   <item><b>Phase 2</b> — resolve the owning <c>GameNightEvent</c> by the tracking-session id
///     and promote that specific draft (<c>Pending → InProgress</c>) via
///     <see cref="GameNightEvent.StartSession"/>, then persist (the aggregate root's xmin write
///     detects a concurrent live-slot race).</item>
///   <item><b>Phase 3</b> — dispatch <see cref="OpenSessionLiveModeCommand"/> LAST, AFTER the
///     promotion is committed, so <c>Session.OpenLiveMode()</c>'s <c>SessionStartedDomainEvent</c>
///     resolves the parent unambiguously and drives the invariante #15 night promotion.</item>
/// </list>
///
/// <para>Because the draft already exists, there is exactly ONE aggregate write here — a single
/// <c>SaveChangesAsync</c> is inherently atomic, so (unlike the create orchestrator) no explicit
/// transaction wrapper is needed.</para>
///
/// <para>Error mapping (never 500):
/// <list type="bullet">
///   <item>Owning night / session not found → <see cref="NotFoundException"/> → 404.</item>
///   <item>Another session already live (in-memory guard) → <see cref="MaxLiveSessionsExceededException"/> → 409.</item>
///   <item>Targeted session not Pending → <see cref="InvalidOperationException"/> re-mapped to
///     <see cref="ConflictException"/> → 409 (parity with the create orchestrator).</item>
///   <item>Concurrent go-live loses the xmin race → <see cref="DbUpdateConcurrencyException"/>
///     re-mapped to <see cref="MaxLiveSessionsExceededException"/> → 409.</item>
///   <item>Concurrent promotion trips the partial-unique live-slot index (Postgres 23505) →
///     re-mapped to <see cref="MaxLiveSessionsExceededException"/> → 409.</item>
/// </list></para>
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

        try
        {
            // Phase 2: promote the targeted draft (Pending → InProgress). NotFound/MaxLive escape this
            // try uncaught (they already map to 404/409); a non-Pending session throws
            // InvalidOperationException which the catch below normalises to a 409.
            gameNight.StartSession(command.SessionId);

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
            // Targeted session not Pending (or a corrupted-state guard) — a conflict, not a 500.
            throw new ConflictException(ex.Message);
        }

        var promoted = gameNight.Sessions.First(s => s.SessionId == command.SessionId);
        var result = new GoLiveSessionResult(
            command.SessionId,
            gameNight.Id,
            promoted.Id,
            promoted.PlayOrder,
            promoted.Status.ToString());

        // Phase 3: open live mode LAST — AFTER the session↔night promotion is committed — so the
        // SessionStartedDomainEvent resolves the parent unambiguously and promotes the night
        // Published → InProgress (invariante #15). Idempotent.
        await _mediator.Send(new OpenSessionLiveModeCommand(command.SessionId), cancellationToken).ConfigureAwait(false);

        return result;
    }

    // True iff the DbUpdateException is the partial-unique-index violation on the GameNight live slot
    // (game_night_sessions InProgress per event), not some other constraint. Uses the shared index-name
    // constant so this catch can never drift from the index declaration.
    private static bool IsGameNightLiveSlotViolation(DbUpdateException ex) =>
        ex.InnerException is Npgsql.PostgresException pg
        && string.Equals(pg.SqlState, Npgsql.PostgresErrorCodes.UniqueViolation, StringComparison.Ordinal)
        && string.Equals(pg.ConstraintName, GameNightSessionEntityConfiguration.UniqueActiveIndexName, StringComparison.Ordinal);
}
