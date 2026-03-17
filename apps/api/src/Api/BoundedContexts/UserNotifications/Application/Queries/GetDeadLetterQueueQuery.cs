using Api.BoundedContexts.UserNotifications.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.UserNotifications.Application.Queries;

/// <summary>
/// Query to retrieve paginated dead letter queue items.
/// Used by admin notification queue dashboard.
/// </summary>
internal record GetDeadLetterQueueQuery(int Page, int PageSize) : IQuery<PaginatedNotificationQueueResult>;
