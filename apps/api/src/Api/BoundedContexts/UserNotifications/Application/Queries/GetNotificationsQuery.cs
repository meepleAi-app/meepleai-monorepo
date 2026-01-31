using Api.BoundedContexts.UserNotifications.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.UserNotifications.Application.Queries;

/// <summary>
/// Query to get notifications for a specific user.
/// Supports filtering by read status and limiting results.
/// </summary>
internal record GetNotificationsQuery(
    Guid UserId,
    bool? UnreadOnly = null,
    int? Limit = 50
) : IQuery<List<NotificationDto>>;
