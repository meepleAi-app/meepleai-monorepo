using Api.BoundedContexts.GameManagement.Application.Commands.GameNights;
using Api.BoundedContexts.GameManagement.Domain.Entities.GameNightEvent;
using Api.Infrastructure;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.GameManagement.Application.Commands.GameNights;

/// <summary>
/// Handles creation of a new game night event.
/// Issue #46: GameNight API endpoints.
/// Issue #43: Returns enriched DTO with OrganizerName.
/// Issue #950 (W1-PR1): after persisting the aggregate, chains to
/// <see cref="CreateGameNightInvitationByEmailCommand"/> for each entry in
/// <see cref="CreateGameNightCommand.InvitedEmails"/> so non-registered guests
/// receive a token-bearing invitation via the existing flow (issue #607 Wave A.5a).
/// </summary>
internal sealed class CreateGameNightCommandHandler : ICommandHandler<CreateGameNightCommand, Guid>
{
    private readonly IGameNightEventRepository _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMediator _mediator;

    public CreateGameNightCommandHandler(
        IGameNightEventRepository repository,
        IUnitOfWork unitOfWork,
        IMediator mediator)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
    }

    public async Task<Guid> Handle(CreateGameNightCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        // Issue #4692: the same creation sent again. The wizard retries on purpose and so does the
        // mutation layer, and the first attempt may have saved the game night and then failed (or lost
        // its response) before the caller heard about it. A game night with the id the caller chose and
        // the same organizer IS that creation: nothing is created again; the invitations are brought to
        // the state the first attempt was heading for. Another organizer's id is a conflict that says
        // nothing about the game night behind it.
        if (command.Id is { } requestedId)
        {
            var existing = await _repository.GetByIdAsync(requestedId, cancellationToken).ConfigureAwait(false);
            if (existing is not null)
            {
                if (existing.OrganizerId != command.UserId)
                {
                    throw new ConflictException("A game night with this id already exists.");
                }

                await InviteByEmailAsync(existing.Id, command, cancellationToken).ConfigureAwait(false);
                return existing.Id;
            }
        }

        var gameNight = GameNightEvent.Create(
            organizerId: command.UserId,
            title: command.Title,
            scheduledAt: command.ScheduledAt,
            description: command.Description,
            location: command.Location,
            maxPlayers: command.MaxPlayers,
            gameIds: command.GameIds,
            id: command.Id,
            timeZoneId: command.TimeZoneId);

        // Pre-create RSVP entries for invited users so PublishHandler sends invitations.
        if (command.InvitedUserIds is { Count: > 0 })
        {
            gameNight.PreInvite(command.InvitedUserIds);
        }

        await _repository.AddAsync(gameNight, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        await InviteByEmailAsync(gameNight.Id, command, cancellationToken).ConfigureAwait(false);

        return gameNight.Id;
    }

    // Issue #950 (W1-PR1): chain token-based invitation creation per email AFTER
    // the game night aggregate has been persisted. Each sub-command persists its
    // own aggregate (separate transaction by design — invitation failures must
    // not roll back the game night). Loop order matches the input list so failures
    // can be correlated to specific emails via logs.
    // Issue #4692: ResendIfPending, because this step may be the repetition of an earlier attempt that
    // already saved an invitation: that one is sent again (same token) instead of being refused.
    private async Task InviteByEmailAsync(
        Guid gameNightId,
        CreateGameNightCommand command,
        CancellationToken cancellationToken)
    {
        if (command.InvitedEmails is not { Count: > 0 })
        {
            return;
        }

        foreach (var email in command.InvitedEmails)
        {
            var subCommand = new CreateGameNightInvitationByEmailCommand(
                GameNightId: gameNightId,
                Email: email,
                OrganizerUserId: command.UserId,
                ResendIfPending: true);

            await _mediator.Send(subCommand, cancellationToken).ConfigureAwait(false);
        }
    }
}
