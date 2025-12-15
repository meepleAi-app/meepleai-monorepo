using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.UserNotifications.Application.Queries;

/// <summary>
/// Query to get unread notification count for a user.
/// Optimized for badge display (count only, no full notifications).
/// </summary>
internal record GetUnreadCountQuery(Guid UserId) : IQuery<int>;
