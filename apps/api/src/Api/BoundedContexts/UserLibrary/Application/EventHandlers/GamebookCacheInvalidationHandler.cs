using Api.BoundedContexts.Administration.Domain.Events;
using Api.BoundedContexts.SessionTracking.Domain.Events;
using Api.BoundedContexts.UserLibrary.Application.Queries.Gamebooks;
using Api.Services;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.UserLibrary.Application.EventHandlers;

/// <summary>
/// Issue #1292 (AC-6.2): invalidates the gamebook index cache for a user when
/// events that affect their gamebook list fire. Uses tag-based invalidation
/// (<see cref="GetUserGamebooksQueryHandler.BuildUserTag"/>) for a single
/// <c>RemoveByTagAsync</c> call regardless of the event type.
///
/// Subscribed events:
/// <list type="bullet">
///   <item><see cref="GamebookCampaignCreatedDomainEvent"/> — new campaign appears</item>
///   <item><see cref="GamebookCampaignDeletedDomainEvent"/> — deleted campaign disappears</item>
///   <item><see cref="UserLibraryGameAddedEvent"/> — library entry added (existing event)</item>
/// </list>
///
/// Mirrors convention of
/// <c>Api.BoundedContexts.Administration.Application.EventHandlers.UserActivityCacheInvalidationEventHandler</c>.
///
/// Performance target: invalidation &lt; 10ms (handler is fire-and-forget from
/// the publisher's perspective via MediatR Publish).
/// </summary>
internal sealed class GamebookCacheInvalidationHandler :
    INotificationHandler<GamebookCampaignCreatedDomainEvent>,
    INotificationHandler<GamebookCampaignDeletedDomainEvent>,
    INotificationHandler<UserLibraryGameAddedEvent>
{
    private readonly IHybridCacheService _cache;
    private readonly ILogger<GamebookCacheInvalidationHandler> _logger;

    public GamebookCacheInvalidationHandler(
        IHybridCacheService cache,
        ILogger<GamebookCacheInvalidationHandler> logger)
    {
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public Task Handle(GamebookCampaignCreatedDomainEvent notification, CancellationToken cancellationToken)
    {
        _logger.LogDebug(
            "Gamebook campaign {CampaignId} created → invalidating gamebook index cache for user {UserId}",
            notification.CampaignId, notification.OwnerUserId);
        return InvalidateUserAsync(notification.OwnerUserId, "campaign_created", cancellationToken);
    }

    public Task Handle(GamebookCampaignDeletedDomainEvent notification, CancellationToken cancellationToken)
    {
        _logger.LogDebug(
            "Gamebook campaign {CampaignId} deleted → invalidating gamebook index cache for user {UserId}",
            notification.CampaignId, notification.OwnerUserId);
        return InvalidateUserAsync(notification.OwnerUserId, "campaign_deleted", cancellationToken);
    }

    public Task Handle(UserLibraryGameAddedEvent notification, CancellationToken cancellationToken)
    {
        _logger.LogDebug(
            "Game {GameId} added to library → invalidating gamebook index cache for user {UserId}",
            notification.GameId, notification.UserId);
        return InvalidateUserAsync(notification.UserId, "library_entry_added", cancellationToken);
    }

    private async Task InvalidateUserAsync(Guid userId, string reason, CancellationToken cancellationToken)
    {
        try
        {
            var removed = await _cache
                .RemoveByTagAsync(GetUserGamebooksQueryHandler.BuildUserTag(userId), cancellationToken)
                .ConfigureAwait(false);
            _logger.LogDebug(
                "Gamebook cache invalidation for user {UserId} ({Reason}): removed {Count} entries",
                userId, reason, removed);
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            // Cache invalidation is best-effort. Stale cache will expire on TTL
            // (5 min) so a transient cache error doesn't break the write path.
            _logger.LogWarning(ex,
                "Gamebook cache invalidation failed for user {UserId} ({Reason}); stale until TTL expiry",
                userId, reason);
        }
    }
}
