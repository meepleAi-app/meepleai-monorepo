using Api.BoundedContexts.GameManagement.Application.Queries.GameNights;
using Api.BoundedContexts.GameManagement.Application.Services;
using Api.BoundedContexts.GameManagement.Domain.Entities.GameNightEvent;
using Api.BoundedContexts.GameManagement.Domain.Events;
using MediatR;
using Microsoft.Extensions.Caching.Hybrid;

namespace Api.BoundedContexts.GameManagement.Application.EventHandlers;

/// <summary>
/// Subscribes to <see cref="GameNightInvitationRespondedEvent"/> and performs
/// the post-RSVP fan-out:
/// <list type="number">
///   <item>Invalidate the <c>game-night-invitation:{token}</c> cache tag so
///         the next read of the public surface returns the fresh
///         <c>AlreadyRespondedAs</c>/<c>RespondedAt</c> projection.</item>
///   <item>Send the RSVP confirmation email to the responding guest
///         (D1 a — see spec §5).</item>
/// </list>
/// Issue #607 (Wave A.5a): GameNight token-based RSVP backend extension.
/// </summary>
/// <remarks>
/// <para>
/// Pitfall #2620 — injects raw <see cref="HybridCache"/> (NOT the
/// <c>IHybridCacheService</c> wrapper) so we share the native tag index used
/// by <see cref="GetGameNightInvitationByTokenQueryHandler"/> as producer.
/// </para>
/// <para>
/// Multi-instance deployment caveat: <c>RemoveByTagAsync</c> evicts the L2
/// distributed entry but only the L1 of this process — same caveat documented
/// in legacy <c>VectorDocumentIndexedForKbFlagHandler</c>.
/// </para>
/// </remarks>
internal sealed class GameNightInvitationRespondedHandler
    : INotificationHandler<GameNightInvitationRespondedEvent>
{
    private readonly IGameNightInvitationRepository _invitationRepository;
    private readonly IGameNightEventRepository _gameNightRepository;
    private readonly IGameNightEmailService _emailService;
    private readonly HybridCache _cache;
    private readonly ILogger<GameNightInvitationRespondedHandler> _logger;

    public GameNightInvitationRespondedHandler(
        IGameNightInvitationRepository invitationRepository,
        IGameNightEventRepository gameNightRepository,
        IGameNightEmailService emailService,
        HybridCache cache,
        ILogger<GameNightInvitationRespondedHandler> logger)
    {
        _invitationRepository = invitationRepository ?? throw new ArgumentNullException(nameof(invitationRepository));
        _gameNightRepository = gameNightRepository ?? throw new ArgumentNullException(nameof(gameNightRepository));
        _emailService = emailService ?? throw new ArgumentNullException(nameof(emailService));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task Handle(
        GameNightInvitationRespondedEvent notification,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(notification);

        // Step 1 — cache invalidation (idempotent / cheap).
        var cacheTag = GetGameNightInvitationByTokenQueryHandler.CacheTagPrefix + notification.Token;
        await _cache.RemoveByTagAsync(cacheTag, cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Invalidated cache tag {CacheTag} after invitation {InvitationId} responded as {Status}",
            cacheTag,
            notification.GameNightInvitationId,
            notification.Status);

        // Step 2 — confirmation email (only when the responder said yes; declined
        // guests don't need a confirmation, matching existing user-RSVP behavior).
        if (notification.Status != Domain.Enums.GameNightInvitationStatus.Accepted)
        {
            return;
        }

        var invitation = await _invitationRepository
            .GetByIdAsync(notification.GameNightInvitationId, cancellationToken)
            .ConfigureAwait(false);

        if (invitation is null)
        {
            _logger.LogWarning(
                "Invitation {InvitationId} disappeared between event raise and handler — skipping confirmation email",
                notification.GameNightInvitationId);
            return;
        }

        var gameNight = await _gameNightRepository
            .GetByIdAsync(invitation.GameNightId, cancellationToken)
            .ConfigureAwait(false);

        if (gameNight is null)
        {
            _logger.LogWarning(
                "GameNight {GameNightId} not found for confirmation email of invitation {InvitationId}",
                invitation.GameNightId,
                invitation.Id);
            return;
        }

        var unsubscribeUrl = $"/invites/{invitation.Token}/unsubscribe";

        await _emailService.SendGameNightRsvpConfirmationEmailAsync(
            toEmail: invitation.Email,
            title: gameNight.Title,
            scheduledAt: gameNight.ScheduledAt,
            location: gameNight.Location,
            unsubscribeUrl: unsubscribeUrl,
            ct: cancellationToken).ConfigureAwait(false);
    }
}
