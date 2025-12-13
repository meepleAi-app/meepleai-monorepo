using Api.BoundedContexts.UserNotifications.Application.DTOs;
using Api.BoundedContexts.UserNotifications.Application.Queries;
using Api.BoundedContexts.UserNotifications.Domain.Aggregates;
using Api.BoundedContexts.UserNotifications.Domain.Repositories;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.UserNotifications.Application.Handlers;

/// <summary>
/// Handler for GetNotificationsQuery.
/// Retrieves user notifications with optional filtering.
/// </summary>
public class GetNotificationsQueryHandler : IQueryHandler<GetNotificationsQuery, List<NotificationDto>>
{
    private readonly INotificationRepository _notificationRepository;

    public GetNotificationsQueryHandler(INotificationRepository notificationRepository)
    {
        _notificationRepository = notificationRepository ?? throw new ArgumentNullException(nameof(notificationRepository));
    }

    public async Task<List<NotificationDto>> Handle(GetNotificationsQuery query, CancellationToken cancellationToken)
    {
        var notifications = await _notificationRepository.GetByUserIdAsync(
            query.UserId,
            query.UnreadOnly ?? false,
            query.Limit ?? 50,
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
