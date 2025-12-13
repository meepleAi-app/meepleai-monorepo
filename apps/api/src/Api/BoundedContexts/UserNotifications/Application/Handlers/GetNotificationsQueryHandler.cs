using Api.BoundedContexts.UserNotifications.Application.DTOs;
using Api.BoundedContexts.UserNotifications.Application.Queries;
using Api.BoundedContexts.UserNotifications.Domain.Aggregates;
using Api.BoundedContexts.UserNotifications.Domain.Repositories;
using Api.Services;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.UserNotifications.Application.Handlers;

/// <summary>
/// Handler for GetNotificationsQuery.
/// Retrieves user notifications with optional filtering.
/// Enforces configurable maximum page size from system configuration.
/// </summary>
public class GetNotificationsQueryHandler : IQueryHandler<GetNotificationsQuery, List<NotificationDto>>
{
    private readonly INotificationRepository _notificationRepository;
    private readonly IConfigurationService _configService;

    /// <summary>
    /// Default maximum page size if not configured.
    /// </summary>
    private const int DefaultMaxPageSize = 50;

    public GetNotificationsQueryHandler(
        INotificationRepository notificationRepository,
        IConfigurationService configService)
    {
        _notificationRepository = notificationRepository ?? throw new ArgumentNullException(nameof(notificationRepository));
        _configService = configService ?? throw new ArgumentNullException(nameof(configService));
    }

    public async Task<List<NotificationDto>> Handle(GetNotificationsQuery query, CancellationToken cancellationToken)
    {
        // Validate pagination parameters
        if (query.Limit.HasValue && query.Limit.Value < 0)
        {
            throw new ArgumentException("Limit must be non-negative", nameof(query));
        }

        // Get max page size from configuration (defaults to 50)
        var configuredMax = await _configService.GetValueAsync<int?>("Notifications:MaxPageSize", DefaultMaxPageSize).ConfigureAwait(false) ?? DefaultMaxPageSize;

        // Sanitize configuration value - must be positive, fallback to default if invalid
        var maxPageSize = configuredMax > 0 ? configuredMax : DefaultMaxPageSize;

        // Enforce maximum limit - requested limit cannot exceed configured max
        var effectiveLimit = Math.Min(query.Limit ?? DefaultMaxPageSize, maxPageSize);

        var notifications = await _notificationRepository.GetByUserIdAsync(
            query.UserId,
            query.UnreadOnly ?? false,
            effectiveLimit,
            cancellationToken
        ).ConfigureAwait(false);

        return notifications.Select(MapToDto).ToList();
    }

    private static NotificationDto MapToDto(Notification notification)
    {
        return new NotificationDto(
            Id: notification.Id,
            UserId: notification.UserId,
            Type: notification.Type.Value,
            Severity: notification.Severity.Value,
            Title: notification.Title,
            Message: notification.Message,
            Link: notification.Link,
            Metadata: notification.Metadata,
            IsRead: notification.IsRead,
            CreatedAt: notification.CreatedAt,
            ReadAt: notification.ReadAt
        );
    }
}
