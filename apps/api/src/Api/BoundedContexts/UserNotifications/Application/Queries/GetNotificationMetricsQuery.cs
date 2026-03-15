using Api.BoundedContexts.UserNotifications.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.UserNotifications.Application.Queries;

/// <summary>
/// Query to retrieve notification queue metrics grouped by channel and status.
/// Used by admin notification dashboard.
/// </summary>
internal record GetNotificationMetricsQuery() : IQuery<NotificationMetricsDto>;
