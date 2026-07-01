using Api.BoundedContexts.Authentication.Application.Queries;
using Api.BoundedContexts.GameManagement.Domain.Entities.GameNightEvent;
using Api.BoundedContexts.SessionTracking.Application.Commands;
using Api.BoundedContexts.SessionTracking.Application.DTOs;
using Api.BoundedContexts.SessionTracking.Application.Queries;
using Api.BoundedContexts.SessionTracking.Domain.Services;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Domain.ValueObjects;
using Api.SharedKernel.Infrastructure.Persistence;
using MediatR;

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

        // Cross-BC: create the Session carrying the campaign link. GameNight linkage is done
        // below via AddSession (mirroring StartGameNightSessionCommandHandler, which also leaves
        // CreateSessionCommand.GameNightEventId null and links via the aggregate).
        var createResult = await _mediator.Send(new CreateSessionCommand(
            command.CallerUserId,
            campaign.GameRefId,
            "GameSpecific",
            DateTime.UtcNow,
            null,
            participants,
            GamebookCampaignId: command.CampaignId), cancellationToken).ConfigureAwait(false);

        try
        {
            // Both calls are required: AddSession registers the sitting; StartCurrentSession is
            // where the #10 max-1-live guard lives (throws MaxLiveSessionsExceededException).
            var gns = gameNight.AddSession(createResult.SessionId, campaign.GameRefId, campaign.Title);
            gameNight.StartCurrentSession();

            await _repository.UpdateAsync(gameNight, cancellationToken).ConfigureAwait(false);
            await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            await _autoSaveScheduler.RegisterAsync(createResult.SessionId, cancellationToken).ConfigureAwait(false);

            return new AttachGamebookCampaignToGameNightResult(
                createResult.SessionId, gns.Id, createResult.SessionCode, gns.PlayOrder);
        }
        catch (InvalidOperationException ex)
        {
            // AddSession's "not Published" guard throws a plain InvalidOperationException → wrap as 409.
            // (StartCurrentSession's #10 guard throws MaxLiveSessionsExceededException, which already
            // IS a ConflictException and maps to 409 via middleware — it propagates past this catch.)
            throw new ConflictException(ex.Message);
        }
    }
}
