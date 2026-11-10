using Api.BoundedContexts.GameManagement.Application.Services;
using Api.BoundedContexts.GameManagement.Domain.Entities.GameNightEvent;
using Api.BoundedContexts.GameManagement.Domain.Enums;
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
/// guests was «not full» (<see cref="GameNightEvent.IsFullWith"/>). #4947: it is taken under the night's
/// seat lock (<see cref="GameNightSeats"/>), inside the transaction that writes the answer, so two answers
/// for the last seat cannot both read it free.
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

        if (gameNight.GetRsvp(command.UserId) is null)
        {
            // #4744: the invitation carries its row version; an answer that overlaps another write is read and
            // decided again, as the token path does. Each attempt is its own transaction, so the seat lock (#4947)
            // is taken and released with it, and the night is re-read inside: a seat decision made on the copy the
            // first attempt already found outdated would be the same wrong decision.
            await InvitationWriteRetry
                .RunAsync(
                    ct => _unitOfWork.ExecuteInTransactionAsync(inner => AnswerAsEmailGuestAsync(command, inner), ct),
                    _events,
                    cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        // The transaction (#3636: through the unit of work, never BeginTransactionAsync) saves and commits for us.
        // A night written in the meantime is refused by its row version and surfaces as 409 concurrent_edit
        // (ADR-060), as every write to the aggregate does.
        await _unitOfWork
            .ExecuteInTransactionAsync(ct => AnswerAsMemberAsync(command, gameNight, ct), cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task AnswerAsMemberAsync(RespondToGameNightCommand command, GameNightEvent gameNight, CancellationToken cancellationToken)
    {
        var rsvp = gameNight.GetRsvp(command.UserId)
            ?? throw new NotFoundException("GameNightRsvp", $"EventId={gameNight.Id}, UserId={command.UserId}");

        switch (command.Response)
        {
            case RsvpStatus.Accepted:
                if (rsvp.Status != RsvpStatus.Accepted && gameNight.MaxPlayers.HasValue)
                {
                    // Under the lock, the night as it is NOW: the copy read before it may be short of a seat. The
                    // member's own row is read again with it — if a twin request of theirs took the seat meanwhile,
                    // their seat is one of the counted ones and must not bounce them.
                    int acceptedGuests;
                    (gameNight, acceptedGuests) = await GameNightSeats
                        .ReadUnderLockAsync(gameNight.Id, _repository, _invitations, cancellationToken)
                        .ConfigureAwait(false);
                    rsvp = gameNight.GetRsvp(command.UserId)
                        ?? throw new NotFoundException("GameNightRsvp", $"EventId={gameNight.Id}, UserId={command.UserId}");

                    if (rsvp.Status != RsvpStatus.Accepted && gameNight.IsFullWith(acceptedGuests))
                        throw GameNightSeats.Full();
                }

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
    }

    private async Task AnswerAsEmailGuestAsync(RespondToGameNightCommand command, CancellationToken cancellationToken)
    {
        var gameNight = await _repository.GetByIdAsync(command.GameNightId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException("GameNightEvent", command.GameNightId.ToString());

        var invitation = await _invitations
            .GetAnsweredByUserAsync(gameNight.Id, command.UserId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException("GameNightRsvp", $"EventId={gameNight.Id}, UserId={command.UserId}");

        // The same contract as the email link (#4856): a withdrawn or expired invitation, or a cancelled night,
        // is 410 Gone from either door, not a 409 with an internal message.
        if (invitation.Status is GameNightInvitationStatus.Cancelled or GameNightInvitationStatus.Expired)
            throw new GoneException($"This invitation is no longer available (status: {invitation.Status}).");
        if (gameNight.Status == GameNightStatus.Cancelled)
            throw new GoneException("This game night was cancelled: the invitation can no longer be answered.");

        var utcNow = _timeProvider.GetUtcNow();
        bool transitioned;
        try
        {
            switch (command.Response)
            {
                case RsvpStatus.Accepted:
                    // Their own seat is already in the accepted-guest count when the invitation is Accepted; only
                    // a change of mind competes for a seat, and competes under the lock (#4947).
                    if (invitation.Status != GameNightInvitationStatus.Accepted && gameNight.MaxPlayers.HasValue)
                    {
                        int acceptedGuests;
                        (gameNight, acceptedGuests) = await GameNightSeats
                            .ReadUnderLockAsync(gameNight.Id, _repository, _invitations, cancellationToken)
                            .ConfigureAwait(false);
                        if (gameNight.IsFullWith(acceptedGuests))
                            throw GameNightSeats.Full();
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

        // No RsvpReceived event here, as on the token path: that event is the aggregate's, and the aggregate is
        // not written. The invitation raises its own (GameNightInvitationRespondedEvent), which tells the
        // organizer (#4947) and sends the guest their confirmation. The transaction saves and commits.
        await _invitations.UpdateAsync(invitation, cancellationToken).ConfigureAwait(false);
    }
}
