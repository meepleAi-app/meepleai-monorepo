using Api.BoundedContexts.GameManagement.Application.Commands.GameNights;
using Api.BoundedContexts.GameManagement.Application.Services;
using Api.BoundedContexts.GameManagement.Domain.Entities.GameNightEvent;
using Api.BoundedContexts.GameManagement.Domain.Enums;
using Api.BoundedContexts.GameManagement.Domain.Events;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Application.Services;
using Api.SharedKernel.Infrastructure.Persistence;

namespace Api.BoundedContexts.GameManagement.Application.Commands.GameNights;

/// <summary>
/// Handles RSVP responses to game night invitations.
/// Issue #46: GameNight API endpoints.
/// Issue #43: Uses NotFoundException, checks IsFull for Accept, raises domain event via aggregate.
/// <para>
/// #4921: two kinds of guest answer here. A <b>member</b> has an RSVP row on the aggregate and the row
/// takes the answer. An <b>email guest with an account</b> (#4759) has no row: their record is the
/// invitation they answered while logged in, and the answer goes to it — accept or decline, and a
/// change of mind is admitted because the account is the responder (the email link, a public
/// credential, refuses a switch: <see cref="GameNightInvitation.ChangeAnswer"/>), but no «maybe», which
/// the invitation does not record. Nothing is written twice: the counts (#4706) keep summing the
/// invitation once. Before this, the guest got a 404 and the email link was their only way.
/// </para>
/// <para>
/// The seat check counts both: a night with four seats, four accepted members and three accepted
/// guests was «not full» (<see cref="GameNightEvent.IsFullWith"/>).
/// </para>
/// </summary>
internal sealed class RespondToGameNightCommandHandler : ICommandHandler<RespondToGameNightCommand>
{
    /// <summary>The 409 code of a «maybe» from an email guest (#4844 style: lower snake case).</summary>
    internal const string MaybeNotAvailableCode = "email_guest_maybe_not_available";

    private readonly IGameNightEventRepository _repository;
    private readonly IGameNightInvitationRepository _invitations;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDomainEventCollector _events;
    private readonly TimeProvider _timeProvider;

    public RespondToGameNightCommandHandler(
        IGameNightEventRepository repository,
        IGameNightInvitationRepository invitations,
        IUnitOfWork unitOfWork,
        IDomainEventCollector events,
        TimeProvider timeProvider)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _invitations = invitations ?? throw new ArgumentNullException(nameof(invitations));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _events = events ?? throw new ArgumentNullException(nameof(events));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    public async Task Handle(RespondToGameNightCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var gameNight = await _repository.GetByIdAsync(command.GameNightId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException("GameNightEvent", command.GameNightId.ToString());

        var rsvp = gameNight.GetRsvp(command.UserId);
        if (rsvp is null)
        {
            // #4744: the invitation carries its row version; an answer that overlaps another write is read and
            // decided again, as the token path does.
            await InvitationWriteRetry
                .RunAsync(ct => AnswerAsEmailGuestAsync(gameNight, command, ct), _events, cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        switch (command.Response)
        {
            case RsvpStatus.Accepted:
                if (rsvp.Status != RsvpStatus.Accepted && await IsFullAsync(gameNight, cancellationToken).ConfigureAwait(false))
                    throw new ConflictException("This game night is full and cannot accept more players");
                rsvp.Accept();
                break;
            case RsvpStatus.Declined:
                rsvp.Decline();
                break;
            case RsvpStatus.Maybe:
                rsvp.SetMaybe();
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(command), $"Invalid RSVP response: {command.Response}");
        }

        // Use domain event pattern for consistency — dispatched by UnitOfWork after SaveChanges.
        gameNight.AddRsvpReceivedEvent(command.UserId, command.Response, gameNight.OrganizerId);

        await _repository.UpdateAsync(gameNight, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task AnswerAsEmailGuestAsync(
        GameNightEvent gameNight, RespondToGameNightCommand command, CancellationToken cancellationToken)
    {
        var invitation = await _invitations
            .GetAnsweredByUserAsync(gameNight.Id, command.UserId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException("GameNightRsvp", $"EventId={gameNight.Id}, UserId={command.UserId}");

        var utcNow = _timeProvider.GetUtcNow();
        bool transitioned;
        try
        {
            switch (command.Response)
            {
                case RsvpStatus.Accepted:
                    // Their own seat is already in the accepted-guest count when the invitation is Accepted; only
                    // a change of mind competes for a seat.
                    if (invitation.Status != GameNightInvitationStatus.Accepted
                        && await IsFullAsync(gameNight, cancellationToken).ConfigureAwait(false))
                    {
                        throw new ConflictException("This game night is full and cannot accept more players");
                    }

                    transitioned = invitation.ChangeAnswer(GameNightInvitationStatus.Accepted, command.UserId, utcNow);
                    break;
                case RsvpStatus.Declined:
                    transitioned = invitation.ChangeAnswer(GameNightInvitationStatus.Declined, command.UserId, utcNow);
                    break;
                case RsvpStatus.Maybe:
                    throw new ConflictException(
                        MaybeNotAvailableCode,
                        "A guest invited by email can accept or decline; the invitation does not record «maybe».");
                default:
                    throw new ArgumentOutOfRangeException(nameof(command), $"Invalid RSVP response: {command.Response}");
            }
        }
        catch (InvalidOperationException ex)
        {
            // The invitation's own rules: cancelled or expired, or not this account's answer to change.
            throw new ConflictException(ex.Message, ex);
        }

        if (!transitioned)
            return;

        // No RsvpReceived event here, as on the token path: that event is the aggregate's, and the
        // aggregate is not written. The guest's confirmation email is the invitation's own flow.
        await _invitations.UpdateAsync(invitation, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task<bool> IsFullAsync(GameNightEvent gameNight, CancellationToken cancellationToken)
    {
        if (!gameNight.MaxPlayers.HasValue)
            return false;

        var acceptedGuests = await _invitations
            .CountAcceptedByGameNightIdAsync(gameNight.Id, cancellationToken)
            .ConfigureAwait(false);
        return gameNight.IsFullWith(acceptedGuests);
    }
}
