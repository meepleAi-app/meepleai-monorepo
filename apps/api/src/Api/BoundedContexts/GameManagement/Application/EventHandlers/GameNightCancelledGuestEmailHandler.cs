using Api.BoundedContexts.Authentication.Infrastructure.Persistence;
using Api.BoundedContexts.GameManagement.Application.Services;
using Api.BoundedContexts.GameManagement.Domain.Entities.GameNightEvent;
using Api.BoundedContexts.GameManagement.Domain.Enums;
using Api.BoundedContexts.GameManagement.Domain.Events;
using Api.Infrastructure.DomainEventLog;
using Api.Infrastructure.Security;
using Api.Observability;
using Api.SharedKernel.Services;
using MediatR;

namespace Api.BoundedContexts.GameManagement.Application.EventHandlers;

/// <summary>
/// #4853: when a night is cancelled, the guests who were invited BY ADDRESS are written to. The registered
/// users are told by <c>GameNightCancelledNotificationHandler</c> (UserNotifications) from the ids the event
/// carries; an address is not an account, is not in that payload, and until now nothing else reached it.
/// </summary>
/// <remarks>
/// <para>
/// <b>Who is written to.</b> Every invitation that is still expecting the night: accepted, or pending and not
/// past its expiry — once per address. Not who declined, not who the organizer took off the night, not a lapsed
/// one. The set is read when the event arrives and nothing cancels the invitations with the night, so the status
/// of an invitation here is the one it had when the night was cancelled. If the cancellation ever cascades to
/// them, the event has to carry the addresses: this handler would then see them all as withdrawn.
/// </para>
/// <para>
/// <b>Delivered more than once, written once.</b> The event is queued in the outbox for every event type, not
/// only the registered ones (<c>EventTypeRegistry</c> governs the event LOG): in <c>Hybrid</c> mode (the
/// development setting) it is published inline AND by the outbox processor seconds later, and in
/// <c>OutboxOnly</c> a batch that failed to commit is redelivered whole. SMTP has no remote dedup, so each
/// invitation carries <see cref="GameNightInvitation.CancellationNoticeSentAt"/>, set and saved right after the
/// email goes out (the precedent is <c>RsvpConfirmationSentAt</c>, #1940). A second delivery finds the mark and
/// writes nothing. The narrow window left is two deliveries running at the very same moment; the mark is not a
/// lock. Do not key on <c>EventId</c>: it does not survive the outbox (#4841).
/// </para>
/// <para>
/// <b>Failures are swallowed on purpose, and counted</b> (the choice of #4839). MediatR runs the handlers of an
/// event in sequence, so a throw would stop the ones after it — the in-app notification of the registered users
/// among them. It is counted in <see cref="MeepleAiMetrics.DomainEventDispatchFailures"/> under this handler's
/// name, because a handler that does not throw is invisible to the dispatcher that usually counts. A failed
/// send leaves no mark, so a later delivery tries that address again.
/// </para>
/// <para>
/// <b>A cancellation of the request is not a failure.</b> <c>EmailService.SendRawEmailAsync</c> wraps EVERY
/// exception, cancellation included, in an <see cref="InvalidOperationException"/>, so the exception type says
/// nothing: what says it is the token. While it is cancelled nothing is swallowed or counted.
/// </para>
/// </remarks>
internal sealed class GameNightCancelledGuestEmailHandler : INotificationHandler<GameNightCancelledEvent>
{
    /// <summary>The name the invitation uses when the organizer's account no longer resolves.</summary>
    private const string FallbackOrganizerName = "A friend";

    private readonly IGameNightInvitationRepository _invitations;
    private readonly IGameNightEventRepository _nights;
    private readonly IUserRepository _users;
    private readonly IGameNightEmailService _emailService;
    private readonly IGameNightGuestEmailOptOutRepository _optOuts;
    private readonly IPublicBaseUrlProvider _publicBaseUrl;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<GameNightCancelledGuestEmailHandler> _logger;

    public GameNightCancelledGuestEmailHandler(
        IGameNightInvitationRepository invitations,
        IGameNightEventRepository nights,
        IUserRepository users,
        IGameNightEmailService emailService,
        IGameNightGuestEmailOptOutRepository optOuts,
        IPublicBaseUrlProvider publicBaseUrl,
        TimeProvider timeProvider,
        ILogger<GameNightCancelledGuestEmailHandler> logger)
    {
        _invitations = invitations ?? throw new ArgumentNullException(nameof(invitations));
        _nights = nights ?? throw new ArgumentNullException(nameof(nights));
        _users = users ?? throw new ArgumentNullException(nameof(users));
        _emailService = emailService ?? throw new ArgumentNullException(nameof(emailService));
        _optOuts = optOuts ?? throw new ArgumentNullException(nameof(optOuts));
        _publicBaseUrl = publicBaseUrl ?? throw new ArgumentNullException(nameof(publicBaseUrl));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task Handle(GameNightCancelledEvent notification, CancellationToken cancellationToken)
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
                    "Game night {GameNightId} not found for the cancellation email to its guests",
                    notification.GameNightEventId);
                return;
            }

            var utcNow = _timeProvider.GetUtcNow();
            var pending = (await _invitations
                    .GetByGameNightIdAsync(notification.GameNightEventId, cancellationToken)
                    .ConfigureAwait(false))
                .Where(i => IsExpectingTheNight(i, utcNow))
                .GroupBy(i => i.Email, StringComparer.OrdinalIgnoreCase)
                // An address already written to (any of its invitations carries the mark) is not written again.
                .Where(g => g.All(i => i.CancellationNoticeSentAt is null))
                .ToList();

            if (pending.Count == 0)
            {
                return;
            }

            var organizer = await _users
                .GetByIdAsync(notification.OrganizerId, cancellationToken)
                .ConfigureAwait(false);
            var organizerName = organizer?.DisplayName ?? FallbackOrganizerName;
            var locale = new GameNightEmailLocale(organizer?.Language, night.TimeZoneId);
            // Resolved once, before the first email: with no public address configured the failure is one counted
            // line here, not one per guest.
            var baseUrl = _publicBaseUrl.GetBaseUrl();

            foreach (var address in pending)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await SendToAsync(notification, night, address.Key, address, organizerName, locale, baseUrl, cancellationToken)
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
                "Failed to write the cancellation email to the guests of game night {GameNightId}",
                notification.GameNightEventId);
        }
#pragma warning restore CA1031
    }

    private async Task SendToAsync(
        GameNightCancelledEvent notification,
        GameNightEvent night,
        string address,
        IEnumerable<GameNightInvitation> invitationsOfTheAddress,
        string organizerName,
        GameNightEmailLocale locale,
        string baseUrl,
        CancellationToken cancellationToken)
    {
        try
        {
            // #4846: an address that asked to stop hearing about game nights is not written to, and not marked:
            // the mark says «written to», which it was not.
            if (await _optOuts.ExistsAsync(address, cancellationToken).ConfigureAwait(false))
            {
                return;
            }

            // Any of the address's invitations can carry it; one written before the column existed has none.
            var unsubscribeKey = invitationsOfTheAddress.Select(i => i.UnsubscribeKey).FirstOrDefault(k => k is not null)
                ?? throw new InvalidOperationException(
                    "None of the invitations of the address has an unsubscribe key: the notice cannot carry the opt-out link.");

            await _emailService.SendGameNightCancelledEmailAsync(
                toEmail: address,
                organizerName: organizerName,
                title: night.Title,
                scheduledAt: night.ScheduledAt,
                unsubscribeUrl: GameNightGuestLinks.Unsubscribe(baseUrl, unsubscribeKey),
                locale: locale,
                ct: cancellationToken).ConfigureAwait(false);

            // Marked per address, right after the email: a failure further on in the list does not take the marks
            // of the addresses already written to with it. Only the mark is written (#4744, #4889): the invitations
            // were read before the first email, and rewriting a copy whole would undo an answer or a withdrawal that
            // arrived while the emails went out.
            var sentAt = _timeProvider.GetUtcNow();
            foreach (var invitation in invitationsOfTheAddress)
            {
                await _invitations.MarkCancellationNoticeSentAsync(invitation.Id, sentAt, cancellationToken).ConfigureAwait(false);
            }
        }
#pragma warning disable CA1031 // Do not catch general exception types
        // One address refused by the transport must not keep the others from being written to.
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            CountFailure(notification);
            _logger.LogError(ex,
                "Failed to write the cancellation email of game night {GameNightId} to {Email}",
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

    private static void CountFailure(GameNightCancelledEvent notification) =>
        MeepleAiMetrics.DomainEventDispatchFailures.Add(
            1,
            new KeyValuePair<string, object?>("event_type", EventTypeRegistry.ResolveOrFullName(notification)),
            new KeyValuePair<string, object?>("handler_name", nameof(GameNightCancelledGuestEmailHandler)));
}
