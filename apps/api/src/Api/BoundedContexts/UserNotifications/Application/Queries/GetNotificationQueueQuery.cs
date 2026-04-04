using Api.BoundedContexts.UserNotifications.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.UserNotifications.Application.Queries;

/// <summary>
/// Query to retrieve paginated notification queue items with optional filtering.
/// Used by admin notification queue dashboard.
/// </summary>
internal record GetNotificationQueueQuery(
    int Page,
    int PageSize,
    string? ChannelFilter,
    string? StatusFilter) : IQuery<PaginatedNotificationQueueResult>;

internal record PaginatedNotificationQueueResult(
    IReadOnlyList<NotificationQueueItemDto> Items,
    int TotalCount,
    int Page,
    int PageSize);
