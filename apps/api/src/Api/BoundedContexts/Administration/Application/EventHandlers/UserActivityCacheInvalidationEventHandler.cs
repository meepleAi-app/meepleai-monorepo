using Api.BoundedContexts.Administration.Domain.Events;
using Api.BoundedContexts.SessionTracking.Domain.Events;
using Api.Observability;
using Api.Services;
using MediatR;

namespace Api.BoundedContexts.Administration.Application.EventHandlers;

/// <summary>
/// Issue #3974: Invalidates user-specific dashboard caches when activity events occur.
/// Uses tag-based invalidation to selectively clear only the affected user's cache entries
/// (dashboard-api, dashboard-insights, activity-timeline).
/// Performance target: invalidation &lt; 10ms.
/// </summary>
internal sealed class UserActivityCacheInvalidationEventHandler :
    INotificationHandler<UserLibraryGameAddedEvent>,
    INotificationHandler<UserGameSessionCompletedEvent>,
    INotificationHandler<UserChatSavedEvent>,
    INotificationHandler<UserWishlistUpdatedEvent>,
    INotificationHandler<SessionFinalizedEvent>
{
    private readonly IHybridCacheService _cache;
    private readonly ILogger<UserActivityCacheInvalidationEventHandler> _logger;

    public UserActivityCacheInvalidationEventHandler(
        IHybridCacheService cache,
        ILogger<UserActivityCacheInvalidationEventHandler> logger)
    {
        _cache = cache;
        _logger = logger;
    }

    public async Task Handle(UserLibraryGameAddedEvent notification, CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Game {GameId} added to library → invalidating dashboard cache for user {UserId}",
            notification.GameId, notification.UserId);

        await InvalidateUserCacheAsync(notification.UserId, "game_added", cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task Handle(UserGameSessionCompletedEvent notification, CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Session {SessionId} completed → invalidating dashboard cache for user {UserId}",
            notification.SessionId, notification.UserId);

        await InvalidateUserCacheAsync(notification.UserId, "session_completed", cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task Handle(UserChatSavedEvent notification, CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Chat {ChatId} saved → invalidating dashboard cache for user {UserId}",
            notification.ChatId, notification.UserId);

        await InvalidateUserCacheAsync(notification.UserId, "chat_saved", cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task Handle(UserWishlistUpdatedEvent notification, CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Wishlist updated for game {GameId} → invalidating dashboard cache for user {UserId}",
            notification.GameId, notification.UserId);

        await InvalidateUserCacheAsync(notification.UserId, "wishlist_updated", cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Handle SessionFinalizedEvent from SessionTracking bounded context.
    /// This event doesn't carry UserId directly, so we invalidate by session tag.
    /// </summary>
    public async Task Handle(SessionFinalizedEvent notification, CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Session {SessionId} finalized → invalidating session-related dashboard caches",
            notification.SessionId);

        try
        {
            // SessionFinalizedEvent doesn't have UserId; invalidate the activity-timeline tag globally
            await _cache.RemoveByTagAsync("activity-timeline", cancellationToken).ConfigureAwait(false);

            RecordMetric("session_finalized");
        }
#pragma warning disable CA1031 // Do not catch general exception types
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to invalidate cache after session finalized: {SessionId}",
                notification.SessionId);
        }
#pragma warning restore CA1031
    }

    /// <summary>
    /// Invalidates all dashboard-related caches for a specific user using tag-based invalidation.
    /// Tags cleared: "user:{userId}" which covers dashboard-api, dashboard-insights, activity-timeline.
    /// </summary>
    private async Task InvalidateUserCacheAsync(
        Guid userId, string trigger, CancellationToken cancellationToken)
    {
        try
        {
            var removed = await _cache.RemoveByTagAsync($"user:{userId}", cancellationToken)
                .ConfigureAwait(false);

            _logger.LogInformation(
                "Dashboard cache invalidated for user {UserId}: {RemovedCount} entries removed (trigger: {Trigger})",
                userId, removed, trigger);

            RecordMetric(trigger);
        }
#pragma warning disable CA1031 // Do not catch general exception types
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to invalidate dashboard cache for user {UserId} (trigger: {Trigger})",
                userId, trigger);
        }
#pragma warning restore CA1031
    }

    private static void RecordMetric(string trigger)
    {
        var tags = new System.Diagnostics.TagList
        {
            { "trigger", trigger },
            { "source", "user_activity" }
        };
        MeepleAiMetrics.DashboardCacheInvalidationsTotal.Add(1, tags);
    }
}
