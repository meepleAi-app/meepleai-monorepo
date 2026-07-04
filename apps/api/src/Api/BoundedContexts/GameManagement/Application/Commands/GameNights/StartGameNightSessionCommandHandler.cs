using Api.BoundedContexts.Authentication.Application.Queries;
using Api.BoundedContexts.GameManagement.Domain.Entities.GameNightEvent;
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
        var participants = await BuildParticipantsAsync(command, cancellationToken).ConfigureAwait(false);

        // WS1 DEC-5 (atomicity): the cross-BC Session INSERT and the aggregate link run in ONE
        // explicit transaction. CreateSessionCommand's inner SaveChangesAsync enlists in this
        // ambient tx (same scoped DbContext) instead of committing on its own, so if the aggregate
        // save loses the xmin race — or any guard trips — the whole thing rolls back and no orphan
        // Session is left behind.
        CreateSessionResult createResult;
        StartGameNightSessionResult result;
        await _unitOfWork.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        // Once CommitTransactionAsync starts, it owns rollback-on-failure (it self-rolls-back and
        // disposes the tx); the catch blocks must not roll back a second time.
        var commitStarted = false;
        try
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
                SkipGameNightEnvelope: true), cancellationToken).ConfigureAwait(false);

            var gns = gameNight.AddSession(createResult.SessionId, command.GameId, command.GameTitle);
            gameNight.StartCurrentSession();

            await _repository.UpdateAsync(gameNight, cancellationToken).ConfigureAwait(false);
            // The xmin race is detected here (aggregate UPDATE with a stale row version), while the
            // tx is still open, so the catch below can roll the Session INSERT back with it.
            await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            commitStarted = true;
            await _unitOfWork.CommitTransactionAsync(cancellationToken).ConfigureAwait(false);

            result = new StartGameNightSessionResult(
                createResult.SessionId, gns.Id, createResult.SessionCode, gns.PlayOrder);
        }
        catch (DbUpdateConcurrencyException)
        {
            // WS1 DEC-5: the xmin loser rolls the whole tx (incl. the Session INSERT) back — no
            // orphan — then maps to the same blocked-modal 409 instead of an uncaught 500.
            if (!commitStarted)
                await _unitOfWork.RollbackTransactionAsync(cancellationToken).ConfigureAwait(false);
            throw new MaxLiveSessionsExceededException(command.GameNightId);
        }
        catch (InvalidOperationException ex)
        {
            if (!commitStarted)
                await _unitOfWork.RollbackTransactionAsync(cancellationToken).ConfigureAwait(false);
            throw new ConflictException(ex.Message);
        }
        catch (Exception)
        {
            // Any other post-INSERT failure (incl. MaxLiveSessionsExceededException from
            // StartCurrentSession's #10 guard) rolls back so the Session is never orphaned;
            // the original exception then propagates unchanged.
            if (!commitStarted)
                await _unitOfWork.RollbackTransactionAsync(cancellationToken).ConfigureAwait(false);
            throw;
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
        StartGameNightSessionCommand command, CancellationToken cancellationToken)
    {
        if (command.Participants is not null && command.Participants.Count > 0)
        {
            if (!command.Participants.Any(p => p.IsOwner))
                throw new ConflictException("At least one participant must be the session owner.");
            return command.Participants.ToList();
        }

        // Auto-seed: look up organizer via Authentication BC (cross-BC MediatR dispatch)
        var organizer = await _mediator.Send(new GetUserByIdQuery(command.UserId), cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException("User", command.UserId.ToString());

        var displayName = !string.IsNullOrWhiteSpace(organizer.DisplayName)
            ? organizer.DisplayName
            : organizer.Email;

        return new List<ParticipantDto>
        {
            new()
            {
                Id = Guid.NewGuid(),
                UserId = command.UserId,
                DisplayName = displayName,
                IsOwner = true,
                JoinOrder = 0
            }
        };
    }
}
