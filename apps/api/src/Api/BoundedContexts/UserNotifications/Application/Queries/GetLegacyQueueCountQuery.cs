using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.UserNotifications.Application.Queries;

/// <summary>
/// Query to get the count of remaining legacy EmailQueueItem rows.
/// Used as a migration gate to track progress from email-only to unified notification queue.
/// </summary>
internal record GetLegacyQueueCountQuery() : IQuery<int>;
