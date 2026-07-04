using Api.BoundedContexts.Authentication.Application.Queries;
using Api.BoundedContexts.GameManagement.Domain.Entities.GameNightEvent;
using Api.BoundedContexts.GameManagement.Domain.Exceptions;
using Api.BoundedContexts.SessionTracking.Application.Commands;
using Api.BoundedContexts.SessionTracking.Application.DTOs;
using Api.BoundedContexts.SessionTracking.Application.Queries;
using Api.BoundedContexts.SessionTracking.Domain.Services;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Domain.ValueObjects;
using Api.SharedKernel.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.GameManagement.Application.Commands.GameNights;

/// <summary>
/// Handles <see cref="AttachGamebookCampaignToGameNightCommand"/> — the "writer" of the
/// Session ↔ GamebookCampaign link (#2632 SI-1b). Mirrors <see cref="StartGameNightSessionCommandHandler"/>:
/// dispatches <see cref="CreateSessionCommand"/> to SessionTracking (now carrying the campaign link),
/// then attaches + starts the resulting session on the GameNight aggregate so #15/#10 fire.
/// </summary>
internal sealed class AttachGamebookCampaignToGameNightCommandHandler
    : ICommandHandler<AttachGamebookCampaignToGameNightCommand, AttachGamebookCampaignToGameNightResult>
{
    private readonly IGameNightEventRepository _repository;
    private readonly IMediator _mediator;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAutoSaveSchedulerService _autoSaveScheduler;

    public AttachGamebookCampaignToGameNightCommandHandler(
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

    public async Task<AttachGamebookCampaignToGameNightResult> Handle(
        AttachGamebookCampaignToGameNightCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        // Load the campaign; the query throws NotFound if missing and Forbidden if the caller
        // is not the campaign owner (ownership guard is handled there).
        var campaign = await _mediator.Send(
            new GetGamebookCampaignQuery(command.CampaignId, command.CallerUserId), cancellationToken)
            .ConfigureAwait(false);

        // D-SHARED: only shared-game campaigns can be played in a GameNight — Session.GameId
        // FK-restricts to shared_games, so a private-game campaign would violate the FK.
        if (campaign.GameRefKind != (int)GameRefKind.Shared)
            throw new ConflictException(
                "Only shared-game libro-game campaigns can be played in a game night.");

        var gameNight = await _repository.GetByIdAsync(command.GameNightId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException("GameNightEvent", command.GameNightId.ToString());

        if (gameNight.OrganizerId != command.CallerUserId)
            throw new ForbiddenException("Only the organizer can attach a campaign to this game night.");

        // WS1 DEC-4/DEC-8: guard-before-create — reject a blocked 2nd-open (409) before the
        // cross-BC CreateSessionCommand commits a durable (orphan) tracking Session.
        gameNight.EnsureCanStartSession();

        // Resolve the caller's real display name (mirrors StartGameNightSessionCommandHandler);
        // a hardcoded literal would persist to session_participants and surface in the player list.
        var caller = await _mediator.Send(new GetUserByIdQuery(command.CallerUserId), cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException("User", command.CallerUserId.ToString());
        var displayName = !string.IsNullOrWhiteSpace(caller.DisplayName) ? caller.DisplayName : caller.Email;

        // Seed the caller as the sole owner participant (a gamebook sitting is typically solo).
        var participants = new List<ParticipantDto>
        {
            new()
            {
                Id = Guid.NewGuid(),
                UserId = command.CallerUserId,
                DisplayName = displayName,
                IsOwner = true,
                JoinOrder = 0
            }
        };

        // WS1 DEC-5/DEC-8 (atomicity): create the Session carrying the campaign link and attach it
        // to the aggregate in ONE explicit transaction. CreateSessionCommand's inner SaveChangesAsync
        // enlists in this ambient tx (same scoped DbContext), so if the aggregate save loses the xmin
        // race — or a guard trips — the Session INSERT rolls back with it and no orphan is left.
        // Mirrors StartGameNightSessionCommandHandler.
        CreateSessionResult createResult;
        AttachGamebookCampaignToGameNightResult result;
        await _unitOfWork.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        // Once CommitTransactionAsync starts it owns rollback-on-failure (self-rolls-back + disposes),
        // so the catch blocks must not roll back a second time.
        var commitStarted = false;
        try
        {
            // WS1 DEC-3/DEC-8: SkipGameNightEnvelope=true — the aggregate is the sole linker (no phantom night).
            createResult = await _mediator.Send(new CreateSessionCommand(
                command.CallerUserId,
                campaign.GameRefId,
                "GameSpecific",
                DateTime.UtcNow,
                null,
                participants,
                GamebookCampaignId: command.CampaignId,
                SkipGameNightEnvelope: true), cancellationToken).ConfigureAwait(false);

            // Both calls are required: AddSession registers the sitting; StartCurrentSession is
            // where the #10 max-1-live guard lives (throws MaxLiveSessionsExceededException).
            var gns = gameNight.AddSession(createResult.SessionId, campaign.GameRefId, campaign.Title);
            gameNight.StartCurrentSession();

            await _repository.UpdateAsync(gameNight, cancellationToken).ConfigureAwait(false);
            await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            commitStarted = true;
            await _unitOfWork.CommitTransactionAsync(cancellationToken).ConfigureAwait(false);

            result = new AttachGamebookCampaignToGameNightResult(
                createResult.SessionId, gns.Id, createResult.SessionCode, gns.PlayOrder);
        }
        catch (DbUpdateConcurrencyException)
        {
            // WS1 DEC-5/DEC-8: the xmin loser rolls the whole tx (incl. the Session INSERT) back —
            // no orphan — then maps to the blocked-modal 409.
            if (!commitStarted)
                await _unitOfWork.RollbackTransactionAsync(cancellationToken).ConfigureAwait(false);
            throw new MaxLiveSessionsExceededException(command.GameNightId);
        }
        catch (InvalidOperationException ex)
        {
            // AddSession's "not Published" guard throws a plain InvalidOperationException → wrap as 409.
            if (!commitStarted)
                await _unitOfWork.RollbackTransactionAsync(cancellationToken).ConfigureAwait(false);
            throw new ConflictException(ex.Message);
        }
        catch (Exception)
        {
            // Any other post-INSERT failure (incl. StartCurrentSession's #10 MaxLiveSessionsExceededException)
            // rolls back so the Session is never orphaned; the original exception then propagates.
            if (!commitStarted)
                await _unitOfWork.RollbackTransactionAsync(cancellationToken).ConfigureAwait(false);
            throw;
        }

        // WS1 DEC-1/DEC-8 (#2647): open live mode LAST, after the link is committed, so the
        // GameNight promotes Published → InProgress. Idempotent.
        await _mediator.Send(new OpenSessionLiveModeCommand(createResult.SessionId), cancellationToken).ConfigureAwait(false);

        await _autoSaveScheduler.RegisterAsync(createResult.SessionId, cancellationToken).ConfigureAwait(false);

        return result;
    }
}
