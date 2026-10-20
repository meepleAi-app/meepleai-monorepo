using Api.BoundedContexts.Administration.Domain.Events;
using Api.BoundedContexts.SessionTracking.Domain.Events;
using Api.Observability;
using MediatR;
using Microsoft.Extensions.Caching.Hybrid;

namespace Api.BoundedContexts.Administration.Application.EventHandlers;

/// <summary>
/// Issue #3974: Invalidates user-specific dashboard caches when activity events occur.
/// Uses tag-based invalidation to selectively clear only the affected user's cache entries
/// (dashboard-api, dashboard-insights, activity-timeline).
/// Performance target: invalidation &lt; 10ms.
/// </summary>
/// <remarks>
/// #4559 — l'invalidazione passa da <see cref="HybridCache.RemoveByTagAsync(string, CancellationToken)"/>
/// di Microsoft, non da <c>IHybridCacheService.RemoveByTagAsync</c>. Il wrapper rimuove solo le chiavi
/// elencate nel suo set Redis <c>cache:tag:{tag}</c>, che si riempie solo quando la voce passa da lui:
/// le voci scritte con <see cref="HybridCache"/> grezzo (gli insights della dashboard, la dashboard V1,
/// <c>UserDashboardService</c>) non c'erano, e restavano vecchie fino alla scadenza. Il tag di
/// <see cref="HybridCache"/> le raggiunge tutte, comprese quelle del wrapper, che passa i suoi tag a
/// <see cref="HybridCache"/>; e funziona anche senza Redis. Il set del wrapper non viene svuotato:
/// le chiavi che vi restano non sono un problema di correttezza, perché rimuovere una chiave assente
/// non fa nulla e una chiave riscritta porta di nuovo il tag.
/// </remarks>
internal sealed class UserActivityCacheInvalidationEventHandler :
    INotificationHandler<UserLibraryGameAddedEvent>,
    INotificationHandler<UserGameSessionCompletedEvent>,
    INotificationHandler<UserChatSavedEvent>,
    INotificationHandler<UserWishlistUpdatedEvent>,
    INotificationHandler<SessionFinalizedEvent>
{
    private const string ActivityTimelineTag = "activity-timeline";

    private readonly HybridCache _cache;
    private readonly ILogger<UserActivityCacheInvalidationEventHandler> _logger;

    public UserActivityCacheInvalidationEventHandler(
        HybridCache cache,
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
            await _cache.RemoveByTagAsync(ActivityTimelineTag, cancellationToken).ConfigureAwait(false);

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
    /// <remarks>
    /// #4559 — <see cref="HybridCache.RemoveByTagAsync(string, CancellationToken)"/> non dice quante voci
    /// ha rimosso: segna il tag come invalidato, e le voci che lo portano diventano vecchie alla lettura.
    /// Il log riporta quindi il tag, non un conteggio.
    /// </remarks>
    private async Task InvalidateUserCacheAsync(
        Guid userId, string trigger, CancellationToken cancellationToken)
    {
        var userTag = $"user:{userId}";
        try
        {
            await _cache.RemoveByTagAsync(userTag, cancellationToken).ConfigureAwait(false);

            _logger.LogInformation(
                "Dashboard cache invalidated for user {UserId}: tag {CacheTag} (trigger: {Trigger})",
                userId, userTag, trigger);

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
