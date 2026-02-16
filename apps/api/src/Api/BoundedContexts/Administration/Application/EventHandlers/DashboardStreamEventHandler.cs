using Api.BoundedContexts.Administration.Domain.Events;
using Api.BoundedContexts.Administration.Domain.Services;
using Api.BoundedContexts.SystemConfiguration.Domain.Events;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.Administration.Application.EventHandlers;

/// <summary>
/// Issue #3324: Forwards domain events to the SSE dashboard stream.
/// Listens to relevant events and broadcasts them to connected clients.
/// </summary>
internal sealed class DashboardStreamEventHandler :
    INotificationHandler<DashboardStatsUpdatedEvent>,
    INotificationHandler<DashboardActivityEvent>,
    INotificationHandler<DashboardSessionUpdatedEvent>,
    INotificationHandler<DashboardNotificationEvent>,
    INotificationHandler<ConfigurationUpdatedEvent>
{
    private readonly IDashboardStreamService _streamService;
    private readonly ILogger<DashboardStreamEventHandler> _logger;
    private readonly TimeProvider _timeProvider;

    public DashboardStreamEventHandler(
        IDashboardStreamService streamService,
        ILogger<DashboardStreamEventHandler> logger,
        TimeProvider timeProvider)
    {
        _streamService = streamService ?? throw new ArgumentNullException(nameof(streamService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _timeProvider = timeProvider;
    }

    /// <summary>
    /// Handle DashboardStatsUpdatedEvent - broadcast to all subscribers.
    /// </summary>
    public async Task Handle(DashboardStatsUpdatedEvent notification, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(notification);
        _logger.LogDebug("Broadcasting DashboardStatsUpdatedEvent to SSE subscribers");

        await _streamService.PublishEventAsync(notification, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Handle DashboardActivityEvent - broadcast to all subscribers.
    /// </summary>
    public async Task Handle(DashboardActivityEvent notification, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(notification);
        _logger.LogDebug("Broadcasting DashboardActivityEvent ({ActivityType}) to SSE subscribers",
            notification.ActivityType);

        await _streamService.PublishEventAsync(notification, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Handle DashboardSessionUpdatedEvent - broadcast to specific user.
    /// </summary>
    public async Task Handle(DashboardSessionUpdatedEvent notification, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(notification);
        _logger.LogDebug("Broadcasting DashboardSessionUpdatedEvent for session {SessionId} to user {UserId}",
            notification.SessionId,
            notification.UserId);

        // Session updates go to the specific user who owns the session
        await _streamService.PublishEventToUserAsync(notification.UserId, notification, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Handle DashboardNotificationEvent - broadcast to specific user.
    /// </summary>
    public async Task Handle(DashboardNotificationEvent notification, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(notification);
        _logger.LogDebug("Broadcasting DashboardNotificationEvent ({Type}) to user {UserId}",
            notification.Type,
            notification.UserId);

        // Notifications are user-specific
        await _streamService.PublishEventToUserAsync(notification.UserId, notification, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Handle ConfigurationUpdatedEvent - notify dashboard of config changes.
    /// Transforms internal event to dashboard event for SSE.
    /// </summary>
    public async Task Handle(ConfigurationUpdatedEvent notification, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(notification);
        _logger.LogDebug("Broadcasting configuration change ({Key}) to SSE subscribers",
            notification.Key.Value);

        // Transform to activity event
        var activityEvent = new DashboardActivityEvent
        {
            ActivityType = "config_updated",
            Title = $"Configuration updated: {notification.Key.Value}",
            Timestamp = _timeProvider.GetUtcNow().UtcDateTime
        };

        await _streamService.PublishEventAsync(activityEvent, cancellationToken).ConfigureAwait(false);
    }
}
