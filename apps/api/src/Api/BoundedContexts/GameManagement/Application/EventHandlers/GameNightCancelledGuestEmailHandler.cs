using Api.BoundedContexts.Authentication.Infrastructure.Persistence;
using Api.BoundedContexts.GameManagement.Application.Services;
using Api.BoundedContexts.GameManagement.Domain.Entities.GameNightEvent;
using Api.BoundedContexts.GameManagement.Domain.Enums;
using Api.BoundedContexts.GameManagement.Domain.Events;
using Api.Helpers;
using Api.Infrastructure.DomainEventLog;
using Api.Infrastructure.Security;
using Api.Observability;
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
/// <b>Failures are swallowed on purpose, and counted</b> (the choice of #4839). MediatR runs the handlers of an
/// event in sequence, so a throw would stop the ones after it — the in-app notification of the registered users
/// among them. It is counted in <see cref="MeepleAiMetrics.DomainEventDispatchFailures"/> under this handler's
/// name, because a handler that does not throw is invisible to the dispatcher that usually counts. A
/// cancellation of the request is not a failure and goes on.
/// </para>
/// <para>
/// <b>No marker of what was sent.</b> The event is not in the outbox registry, so it is published once, in
/// memory, after the save: there is no redelivery to guard against. If it ever moves to the outbox, the
/// precedent for a per-invitation marker is <c>RsvpConfirmationSentAt</c> (#1940); it costs a column and a
/// migration. Do not key on <c>EventId</c> until it survives the outbox (#4841).
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
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<GameNightCancelledGuestEmailHandler> _logger;

    public GameNightCancelledGuestEmailHandler(
        IGameNightInvitationRepository invitations,
        IGameNightEventRepository nights,
        IUserRepository users,
        IGameNightEmailService emailService,
        TimeProvider timeProvider,
        ILogger<GameNightCancelledGuestEmailHandler> logger)
    {
        _invitations = invitations ?? throw new ArgumentNullException(nameof(invitations));
        _nights = nights ?? throw new ArgumentNullException(nameof(nights));
        _users = users ?? throw new ArgumentNullException(nameof(users));
        _emailService = emailService ?? throw new ArgumentNullException(nameof(emailService));
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
            var addresses = (await _invitations
                    .GetByGameNightIdAsync(notification.GameNightEventId, cancellationToken)
                    .ConfigureAwait(false))
                .Where(i => IsExpectingTheNight(i, utcNow))
                .Select(i => i.Email)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (addresses.Count == 0)
            {
                return;
            }

            var organizer = await _users
                .GetByIdAsync(notification.OrganizerId, cancellationToken)
                .ConfigureAwait(false);
            var organizerName = organizer?.DisplayName ?? FallbackOrganizerName;
            var locale = new GameNightEmailLocale(organizer?.Language, night.TimeZoneId);

            foreach (var address in addresses)
            {
                await SendToAsync(notification, night, address, organizerName, locale, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            // The caller leaving (a shutdown, an aborted request in the in-memory path) is not a failure to
            // swallow or to count.
            throw;
        }
#pragma warning disable CA1031 // Do not catch general exception types
        // SERVICE BOUNDARY: EVENT HANDLER PATTERN - the guests' email must not block the OTHER handlers of the event.
        catch (Exception ex)
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
        string organizerName,
        GameNightEmailLocale locale,
        CancellationToken cancellationToken)
    {
        try
        {
            await _emailService.SendGameNightCancelledEmailAsync(
                toEmail: address,
                organizerName: organizerName,
                title: night.Title,
                scheduledAt: night.ScheduledAt,
                locale: locale,
                ct: cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
#pragma warning disable CA1031 // Do not catch general exception types
        // One address refused by the transport must not keep the others from being written to.
        catch (Exception ex)
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
