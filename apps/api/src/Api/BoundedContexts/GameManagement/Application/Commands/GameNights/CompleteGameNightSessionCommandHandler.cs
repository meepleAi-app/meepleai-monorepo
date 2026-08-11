using Api.BoundedContexts.GameManagement.Domain.Entities.GameNightEvent;
using Api.BoundedContexts.SessionTracking.Application.Commands;
using Api.BoundedContexts.SessionTracking.Domain.Repositories;
using Api.BoundedContexts.SessionTracking.Domain.Services;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.GameManagement.Application.Commands.GameNights;

/// <summary>
/// Handles completing the current in-progress session within a game night (#2634 C4).
/// Atomically: completes the GameNightSession (setting the winner) AND finalizes the correlated
/// tracking Session via <see cref="FinalizeSessionCommand"/> — so the tracking Session doesn't
/// stay live (no orphan / no diverging winner) and its finalize events fire. The WinnerId is
/// write-validated as a participant of the session (409, never a silent no-winner).
/// </summary>
internal sealed class CompleteGameNightSessionCommandHandler : ICommandHandler<CompleteGameNightSessionCommand>
{
    private readonly IGameNightEventRepository _repository;
    private readonly ISessionRepository _sessionRepository;
    private readonly IMediator _mediator;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAutoSaveSchedulerService _autoSaveScheduler;

    public CompleteGameNightSessionCommandHandler(
        IGameNightEventRepository repository,
        ISessionRepository sessionRepository,
        IMediator mediator,
        IUnitOfWork unitOfWork,
        IAutoSaveSchedulerService autoSaveScheduler)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _sessionRepository = sessionRepository ?? throw new ArgumentNullException(nameof(sessionRepository));
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _autoSaveScheduler = autoSaveScheduler ?? throw new ArgumentNullException(nameof(autoSaveScheduler));
    }

    public async Task Handle(CompleteGameNightSessionCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var gameNight = await _repository.GetByIdAsync(command.GameNightId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException("GameNightEvent", command.GameNightId.ToString());

        if (gameNight.OrganizerId != command.UserId)
            throw new ForbiddenException("Only the organizer can complete sessions.");

        var currentSession = gameNight.CurrentSession
            ?? throw new ConflictException("No in-progress session to complete.");
        var trackingSessionId = currentSession.SessionId;

        // Load the tracking Session up front to (a) write-validate the winner and (b) build the
        // finalize ranks — a WinnerId that is not a participant is a 409, not a silent no-winner
        // (panel must-fix #3).
        var session = await _sessionRepository.GetByIdAsync(trackingSessionId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException("Session", trackingSessionId.ToString());

        if (command.WinnerId.HasValue && session.Participants.All(p => p.Id != command.WinnerId.Value))
            throw new ConflictException("Winner must be a participant of the session.");

        // FinalizeSessionCommand requires a rank for every participant: winner = 1, everyone else = 2;
        // no winner → all 2 (no rank-1 → the finalize event records no winner).
        var finalRanks = session.Participants.ToDictionary(
            p => p.Id,
            p => command.WinnerId.HasValue && p.Id == command.WinnerId.Value ? 1 : 2);

        // #3636: ExecuteInTransactionAsync — BeginTransactionAsync lancia sotto la retry strategy
        // attiva fuori da Testing. Rollback e commit sono ora della UoW, quindi `commitStarted` e i
        // rollback manuali spariscono; i catch restano solo per mappare l'eccezione.
        try
        {
            await _unitOfWork.ExecuteInTransactionAsync(async ct =>
            {
                gameNight.CompleteCurrentSession(command.WinnerId);
                await _repository.UpdateAsync(gameNight, ct).ConfigureAwait(false);
                await _unitOfWork.SaveChangesAsync(ct).ConfigureAwait(false);

            // Cross-BC: finalize the tracking Session in the SAME transaction. Session.Finalize
            // enforces Active/Paused; its inner SaveChangesAsync enlists in this ambient tx, so the
            // DB changes (GameNightSession complete + tracking Session finalize) roll back together.
            // KNOWN RISK (accepted, → SI-3-proper): FinalizeSessionCommandHandler's SSE side-effects
            // (_diaryStream.Publish + _syncService.PublishEventAsync) fire before the commit, so a
            // rare commit failure leaves phantom broadcasts — clients self-correct on the next poll.
            // See must-fix #5 in the C4 spec (2026-07-05-issue-2634-c4-winner-completa-design.md).
            // #3636 amplia leggermente quella finestra già accettata: sotto la retry strategy il
            // delegate può essere rieseguito, quindi i broadcast possono ripetersi. Stesso effetto
            // (i client si riallineano), stessa mitigazione; spostarli fuori è SI-3-proper.
            // RequestedBy = session.UserId: this is a trusted internal orchestration path. The caller's
            // authority was already enforced above (organizer check, line 49) — the FinalizeSessionCommand
            // IDOR guard targets direct HTTP callers, so we finalize on behalf of the tracking session's
            // owner to avoid spuriously blocking a legitimate game-night completion.
                await _mediator.Send(new FinalizeSessionCommand(trackingSessionId, finalRanks, session.UserId), ct).ConfigureAwait(false);
            }, cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ConflictException("The session was modified concurrently. Please retry.");
        }
        catch (InvalidOperationException ex)
        {
            throw new ConflictException(ex.Message);
        }

        await _autoSaveScheduler.RemoveAsync(trackingSessionId, cancellationToken).ConfigureAwait(false);
    }
}
