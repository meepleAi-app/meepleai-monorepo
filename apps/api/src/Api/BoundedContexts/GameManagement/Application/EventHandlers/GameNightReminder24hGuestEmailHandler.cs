using Api.BoundedContexts.Authentication.Infrastructure.Persistence;
using Api.BoundedContexts.GameManagement.Application.Services;
using Api.BoundedContexts.GameManagement.Domain.Entities.GameNightEvent;
using Api.BoundedContexts.GameManagement.Domain.Enums;
using Api.BoundedContexts.GameManagement.Domain.Events;
using Api.Infrastructure.DomainEventLog;
using Api.Infrastructure.Security;
using Api.Observability;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.SharedKernel.Services;
using MediatR;

namespace Api.BoundedContexts.GameManagement.Application.EventHandlers;

/// <summary>
/// #4846: the 24 hour reminder, to the guests who were invited BY ADDRESS. <c>GameNightReminderJob</c> emits
/// <see cref="GameNightReminder24hEvent"/> once per night and the registered users are reminded in the app by
/// <c>GameNightReminder24hNotificationHandler</c> (UserNotifications); an address is not an account, is not in that
/// payload, and nothing else reached it. The email that existed for this (<c>SendGameNightReminder24hEmailAsync</c>)
/// had no caller.
/// </summary>
/// <remarks>
/// <para>
/// <b>Who is reminded.</b> Every invitation that still expects the night: accepted, or pending and not past its
/// expiry, once per address. A guest who has not answered is asked to, with the link to do it. Not who declined,
/// not who the organizer took off the night, not a lapsed one, not an address that asked to stop
/// (<see cref="IGameNightGuestEmailOptOutRepository"/>). A night that is no longer on — cancelled, or completed
/// by the time a late delivery runs — gets no reminder: the cancellation email already went out, and a reminder
/// after it would contradict it.
/// </para>
/// <para>
/// <b>Delivered more than once, written once.</b> The event is queued in the outbox for every event type: in
/// <c>Hybrid</c> mode it is published inline AND by the outbox processor seconds later, and in <c>OutboxOnly</c> a
/// batch that failed to commit is redelivered whole. SMTP has no remote dedup, so each invitation carries
/// <see cref="GameNightInvitation.Reminder24hSentAt"/>, set and saved right after the email goes out (the same
/// device as <c>CancellationNoticeSentAt</c>, #4853, and <c>RsvpConfirmationSentAt</c>, #1940). A second delivery
/// finds the mark and writes nothing. The mark is not a lock: two deliveries at the very same moment can still
/// both write. Do not key on <c>EventId</c>: it does not survive the outbox for events built before #4841.
/// </para>
/// <para>
/// <b>Failures are swallowed on purpose, and counted</b> (the choice of #4839): a throw would stop the handlers
/// that come after this one in the same event, among them the in-app reminder of the registered users. They are
/// counted in <see cref="MeepleAiMetrics.DomainEventDispatchFailures"/> under this handler's name. A failed send
/// leaves no mark, so a later delivery tries that address again. A cancellation of the request is not a failure:
/// <c>EmailService.SendRawEmailAsync</c> wraps EVERY exception, cancellation included, in an
/// <see cref="InvalidOperationException"/>, so the token is asked, not the exception type.
/// </para>
/// </remarks>
internal sealed class GameNightReminder24hGuestEmailHandler : INotificationHandler<GameNightReminder24hEvent>
{
    /// <summary>The name the invitation uses when the organizer's account no longer resolves.</summary>
    private const string FallbackOrganizerName = "A friend";

    private readonly IGameNightInvitationRepository _invitations;
    private readonly IGameNightEventRepository _nights;
    private readonly IUserRepository _users;
    private readonly IGameNightEmailService _emailService;
    private readonly IGameNightGuestEmailOptOutRepository _optOuts;
    private readonly IPublicBaseUrlProvider _publicBaseUrl;
    private readonly IUnitOfWork _unitOfWork;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<GameNightReminder24hGuestEmailHandler> _logger;

    public GameNightReminder24hGuestEmailHandler(
        IGameNightInvitationRepository invitations,
        IGameNightEventRepository nights,
        IUserRepository users,
        IGameNightEmailService emailService,
        IGameNightGuestEmailOptOutRepository optOuts,
        IPublicBaseUrlProvider publicBaseUrl,
        IUnitOfWork unitOfWork,
        TimeProvider timeProvider,
        ILogger<GameNightReminder24hGuestEmailHandler> logger)
    {
        _invitations = invitations ?? throw new ArgumentNullException(nameof(invitations));
        _nights = nights ?? throw new ArgumentNullException(nameof(nights));
        _users = users ?? throw new ArgumentNullException(nameof(users));
        _emailService = emailService ?? throw new ArgumentNullException(nameof(emailService));
        _optOuts = optOuts ?? throw new ArgumentNullException(nameof(optOuts));
        _publicBaseUrl = publicBaseUrl ?? throw new ArgumentNullException(nameof(publicBaseUrl));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task Handle(GameNightReminder24hEvent notification, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(notification);

        try
        {
            var night = await _nights
                .GetByIdAsync(notification.GameNightEventId, cancellationToken)
                .ConfigureAwait(false);

            if (night is null)
            {
                _logger.LogWarning(
                    "Game night {GameNightId} not found for the reminder email to its guests",
                    notification.GameNightEventId);
                return;
            }

            if (night.Status != GameNightStatus.Published)
            {
                _logger.LogInformation(
                    "Game night {GameNightId} is {Status} now: no reminder email to its guests",
                    night.Id, night.Status);
                return;
            }

            var utcNow = _timeProvider.GetUtcNow();
            var expecting = (await _invitations
                    .GetByGameNightIdAsync(notification.GameNightEventId, cancellationToken)
                    .ConfigureAwait(false))
                .Where(i => IsExpectingTheNight(i, utcNow))
                .ToList();

            var pending = expecting
                .GroupBy(i => i.Email, StringComparer.OrdinalIgnoreCase)
                // An address already written to (any of its invitations carries the mark) is not written again.
                .Where(g => g.All(i => i.Reminder24hSentAt is null))
                .ToList();

            if (pending.Count == 0)
            {
                return;
            }

            var organizer = await _users
                .GetByIdAsync(night.OrganizerId, cancellationToken)
                .ConfigureAwait(false);
            var organizerName = organizer?.DisplayName ?? FallbackOrganizerName;
            var locale = new GameNightEmailLocale(organizer?.Language, night.TimeZoneId);

            // Registered members who said yes, plus each guest address that did (once: the same person invited twice
            // is one seat).
            var confirmed = night.AcceptedCount + expecting
                .Where(i => i.Status == GameNightInvitationStatus.Accepted)
                .Select(i => i.Email)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count();

            // Resolved once, before the first email: with no public address configured the failure is one counted
            // line here, not one per guest.
            var baseUrl = _publicBaseUrl.GetBaseUrl();

            foreach (var address in pending)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await SendToAsync(notification, night, address.Key, address.ToList(), organizerName, confirmed, locale, baseUrl, cancellationToken)
                    .ConfigureAwait(false);
            }
        }
#pragma warning disable CA1031 // Do not catch general exception types
        // SERVICE BOUNDARY: EVENT HANDLER PATTERN - the guests' email must not block the OTHER handlers of the event.
        // While the token is cancelled nothing is caught: a cancellation is the caller leaving, and it can arrive
        // wrapped (see the remarks), so the token is asked, not the exception type.
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            CountFailure(notification);
            _logger.LogError(ex,
                "Failed to write the reminder email to the guests of game night {GameNightId}",
                notification.GameNightEventId);
        }
#pragma warning restore CA1031
    }

    private async Task SendToAsync(
        GameNightReminder24hEvent notification,
        GameNightEvent night,
        string address,
        IReadOnlyList<GameNightInvitation> invitationsOfTheAddress,
        string organizerName,
        int confirmedCount,
        GameNightEmailLocale locale,
        string baseUrl,
        CancellationToken cancellationToken)
    {
        try
        {
            // An address that asked to stop hearing about game nights is not written to, and not marked: the mark
            // says «written to», which it was not.
            if (await _optOuts.ExistsAsync(address, cancellationToken).ConfigureAwait(false))
            {
                return;
            }

            // One person, possibly two invitations (they accepted, then were invited again): if any of them said yes
            // they are not asked to answer. The answer link is built on the token of the invitation that is still
            // waiting, if there is one. The opt-out link carries the unsubscribe KEY, never the RSVP token (the
            // organizer holds that one); the list is keyed by the address, so the key of any of its invitations
            // works, and one written before the column existed has none.
            var unsubscribeKey = invitationsOfTheAddress.Select(i => i.UnsubscribeKey).FirstOrDefault(k => k is not null)
                ?? throw new InvalidOperationException(
                    "None of the invitations of the address has an unsubscribe key: the reminder cannot carry the opt-out link.");
            var accepted = invitationsOfTheAddress.FirstOrDefault(i => i.Status == GameNightInvitationStatus.Accepted);
            var waiting = accepted is null
                ? invitationsOfTheAddress.FirstOrDefault(i => i.Status == GameNightInvitationStatus.Pending)
                : null;
            var anchor = waiting ?? accepted ?? invitationsOfTheAddress[0];

            await _emailService.SendGameNightReminder24hEmailAsync(
                toEmail: address,
                organizerName: organizerName,
                title: night.Title,
                scheduledAt: night.ScheduledAt,
                location: night.Location,
                confirmedCount: confirmedCount,
                isPending: waiting is not null,
                rsvpUrl: $"{baseUrl}/invites/{anchor.Token}",
                unsubscribeUrl: GameNightGuestLinks.Unsubscribe(baseUrl, unsubscribeKey),
                locale: locale,
                ct: cancellationToken).ConfigureAwait(false);

            // Marked and saved per address, right after the email: a failure further on in the list does not take
            // the marks of the addresses already written to with it.
            var sentAt = _timeProvider.GetUtcNow();
            foreach (var invitation in invitationsOfTheAddress)
            {
                invitation.MarkReminder24hSent(sentAt);
                await _invitations.UpdateAsync(invitation, cancellationToken).ConfigureAwait(false);
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
#pragma warning disable CA1031 // Do not catch general exception types
        // One address refused by the transport must not keep the others from being written to.
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            CountFailure(notification);
            _logger.LogError(ex,
                "Failed to write the reminder email of game night {GameNightId} to {Email}",
                notification.GameNightEventId, DataMasking.MaskEmail(address));
        }
#pragma warning restore CA1031
    }

    /// <summary>
    /// Accepted, or pending with a link that still works. Declined, withdrawn and expired are not waiting for the
    /// night; a pending one past <c>ExpiresAt</c> cannot answer any more (410 on its link) and never said yes.
    /// </summary>
    private static bool IsExpectingTheNight(GameNightInvitation invitation, DateTimeOffset utcNow) =>
        invitation.Status == GameNightInvitationStatus.Accepted
        || (invitation.Status == GameNightInvitationStatus.Pending && !invitation.IsExpired(utcNow));

    private static void CountFailure(GameNightReminder24hEvent notification) =>
        MeepleAiMetrics.DomainEventDispatchFailures.Add(
            1,
            new KeyValuePair<string, object?>("event_type", EventTypeRegistry.ResolveOrFullName(notification)),
            new KeyValuePair<string, object?>("handler_name", nameof(GameNightReminder24hGuestEmailHandler)));
}
