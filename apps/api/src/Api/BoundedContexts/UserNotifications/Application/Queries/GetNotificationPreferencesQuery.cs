using Api.BoundedContexts.UserNotifications.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.UserNotifications.Application.Queries;

/// <summary>
/// Query to retrieve user notification preferences.
/// Issue #4220: Multi-channel notification configuration.
/// </summary>
internal record GetNotificationPreferencesQuery(Guid UserId) : IQuery<NotificationPreferencesDto>;
