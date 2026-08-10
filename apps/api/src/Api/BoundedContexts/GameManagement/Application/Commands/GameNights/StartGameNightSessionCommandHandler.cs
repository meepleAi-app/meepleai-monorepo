using Api.BoundedContexts.Authentication.Application.DTOs;
using Api.BoundedContexts.Authentication.Application.Queries;
using Api.BoundedContexts.GameManagement.Domain.Entities.GameNightEvent;
using Api.BoundedContexts.GameManagement.Domain.Enums;
using Api.BoundedContexts.GameManagement.Domain.Exceptions;
using Api.BoundedContexts.GameToolbox.Application.Commands;
using Api.BoundedContexts.GameToolbox.Application.Queries;
using Api.BoundedContexts.SessionTracking.Application.Commands;
using Api.BoundedContexts.SessionTracking.Application.DTOs;
using Api.BoundedContexts.SessionTracking.Domain.Services;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.GameManagement.Application.Commands.GameNights;

/// <summary>
/// Handles starting a game session within a game night event.
/// Cross-BC communication: dispatches CreateSessionCommand to SessionTracking BC via MediatR,
/// then links the resulting session to the GameNightEvent aggregate.
///
/// If the command provides no participants, the handler auto-seeds the organizer
/// (looked up via Authentication.GetUserByIdQuery) as the sole owner participant.
/// </summary>
internal sealed class StartGameNightSessionCommandHandler : ICommandHandler<StartGameNightSessionCommand, StartGameNightSessionResult>
{
    private readonly IGameNightEventRepository _repository;
    private readonly IMediator _mediator;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAutoSaveSchedulerService _autoSaveScheduler;

    public StartGameNightSessionCommandHandler(
        IGameNightEventRepository repository,
        IMediator mediator,
        IUnitOfWork unitOfWork,
        IAutoSaveSchedulerService autoSaveScheduler)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _autoSaveScheduler = autoSaveScheduler ?? throw new ArgumentNullException(nameof(autoSaveScheduler));
    }

    public async Task<StartGameNightSessionResult> Handle(
        StartGameNightSessionCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var gameNight = await _repository.GetByIdAsync(command.GameNightId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException("GameNightEvent", command.GameNightId.ToString());

        if (gameNight.OrganizerId != command.UserId)
            throw new ForbiddenException("Only the organizer can start sessions.");

        // WS1 DEC-4: guard-before-create. Reject a blocked 2nd-open (409) BEFORE the
        // cross-BC CreateSessionCommand commits a durable (orphan) tracking Session.
        gameNight.EnsureCanStartSession();

        // Build participant list: auto-seed organizer when command provides none.
        var participants = await BuildParticipantsAsync(gameNight, command, cancellationToken).ConfigureAwait(false);

        // WS1 DEC-5 (atomicity): the cross-BC Session INSERT and the aggregate link run in ONE
        // explicit transaction. CreateSessionCommand's inner SaveChangesAsync enlists in this
        // ambient tx (same scoped DbContext) instead of committing on its own, so if the aggregate
        // save loses the xmin race — or any guard trips — the whole thing rolls back and no orphan
        // Session is left behind.
        CreateSessionResult createResult = null!;
        StartGameNightSessionResult result;

        // #3636: ExecuteInTransactionAsync — BeginTransactionAsync lancia sotto la retry strategy
        // attiva fuori da Testing (questo handler rispondeva 500 in ambiente reale). Il rollback è
        // ora della UoW su QUALUNQUE eccezione, quindi il flag `commitStarted` e i tre rollback
        // manuali spariscono: i catch restano solo per mappare l'eccezione, che è il loro scopo.
        // I side effect non transazionali (live mode, autosave, toolbox) erano già fuori e tali
        // restano — la strategy può rieseguire il delegate, loro no.
        try
        {
            result = await _unitOfWork.ExecuteInTransactionAsync(async ct =>
            {
                // WS1 DEC-3: SkipGameNightEnvelope=true — the GameNightEvent aggregate (AddSession
                // below) is the SOLE linker against command.GameNightId, so CreateSessionCommand does
                // NOT mint a phantom ad-hoc night that would double-link the session.
                createResult = await _mediator.Send(new CreateSessionCommand(
                    command.UserId,
                    command.GameId,
                    "GameSpecific",
                    DateTime.UtcNow,
                    null,
                    participants,
                    StateTier: command.StateTier,
                    SkipGameNightEnvelope: true), ct).ConfigureAwait(false);

                var gns = gameNight.AddSession(createResult.SessionId, command.GameId, command.GameTitle);
                gameNight.StartCurrentSession();

                await _repository.UpdateAsync(gameNight, ct).ConfigureAwait(false);
                // The xmin race is detected by the SaveChanges inside ExecuteInTransactionAsync
                // (aggregate UPDATE with a stale row version) while the tx is still open, so the
                // Session INSERT is rolled back with it.

                return new StartGameNightSessionResult(
                    createResult.SessionId, gns.Id, createResult.SessionCode, gns.PlayOrder);
            }, cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateConcurrencyException)
        {
            // WS1 DEC-5: the xmin loser has had the whole tx (incl. the Session INSERT) rolled
            // back — no orphan — and maps to the same blocked-modal 409 instead of an uncaught 500.
            throw new MaxLiveSessionsExceededException(command.GameNightId);
        }
        catch (InvalidOperationException ex)
        {
            throw new ConflictException(ex.Message);
        }

        // WS1 DEC-1 (#2647): open live mode LAST — AFTER the session↔night link is
        // committed — so the SessionStartedDomainEvent resolves the parent unambiguously
        // and promotes the night Published → InProgress. Idempotent.
        await _mediator.Send(new OpenSessionLiveModeCommand(createResult.SessionId), cancellationToken).ConfigureAwait(false);

        await _autoSaveScheduler.RegisterAsync(createResult.SessionId, cancellationToken).ConfigureAwait(false);

        // Best-effort fire-and-forget: detach from the handler's CancellationToken so the
        // warm-up does not block the response and is not cancelled on client disconnect.
        _ = TryApplyToolboxTemplateAsync(command.GameId, CancellationToken.None);

        return result;
    }

    private async Task TryApplyToolboxTemplateAsync(Guid gameId, CancellationToken cancellationToken)
    {
        try
        {
            var templates = await _mediator.Send(
                new GetToolboxTemplatesQuery(GameId: gameId), cancellationToken)
                .ConfigureAwait(false);

            var template = templates.FirstOrDefault();
            if (template is null) return;

            await _mediator.Send(
                new ApplyToolboxTemplateCommand(template.Id, gameId), cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Toolbox warm-up is best-effort: never propagate exceptions.
        }
    }

    private async Task<List<ParticipantDto>> BuildParticipantsAsync(
        GameNightEvent gameNight, StartGameNightSessionCommand command, CancellationToken cancellationToken)
    {
        if (command.Participants is not null && command.Participants.Count > 0)
        {
            if (!command.Participants.Any(p => p.IsOwner))
                throw new ConflictException("At least one participant must be the session owner.");
            return command.Participants.ToList();
        }

        // #2634 C4: seed the roster from the night — the organizer as owner (JoinOrder 0) plus every
        // ACCEPTED-RSVP player. This replaces the WS1 organizer-only auto-seed so a real night carries
        // its confirmed players (and the winner picker has real candidates). RSVPs are User-linked, so
        // this seeds Users only; a guest-player mechanism is out of C4 scope.
        var organizer = await _mediator.Send(new GetUserByIdQuery(command.UserId), cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException("User", command.UserId.ToString());

        var participants = new List<ParticipantDto>
        {
            new()
            {
                Id = Guid.NewGuid(),
                UserId = command.UserId,
                DisplayName = DisplayNameOf(organizer),
                IsOwner = true,
                JoinOrder = 0
            }
        };

        var acceptedPlayerIds = gameNight.Rsvps
            .Where(r => r.Status == RsvpStatus.Accepted && r.UserId != command.UserId)
            .Select(r => r.UserId)
            .Distinct()
            .ToList();

        var joinOrder = 1;
        foreach (var userId in acceptedPlayerIds)
        {
            var player = await _mediator.Send(new GetUserByIdQuery(userId), cancellationToken).ConfigureAwait(false);
            if (player is null) continue; // an accepted RSVP whose user no longer resolves is skipped, not fatal

            participants.Add(new ParticipantDto
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                DisplayName = DisplayNameOf(player),
                IsOwner = false,
                JoinOrder = joinOrder++
            });
        }

        return participants;
    }

    private static string DisplayNameOf(UserDto user) =>
        !string.IsNullOrWhiteSpace(user.DisplayName) ? user.DisplayName : user.Email;
}
