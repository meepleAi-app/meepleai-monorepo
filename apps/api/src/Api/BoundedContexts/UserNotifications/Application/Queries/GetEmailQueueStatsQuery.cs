using Api.BoundedContexts.UserNotifications.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.UserNotifications.Application.Queries;

/// <summary>
/// Query to get email queue statistics for admin monitoring.
/// Issue #4430: Email queue dashboard monitoring.
/// </summary>
internal record GetEmailQueueStatsQuery() : IQuery<EmailQueueStatsDto>;
