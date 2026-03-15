using Api.BoundedContexts.UserNotifications.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.UserNotifications.Application.Queries;

/// <summary>
/// Query to retrieve the current Slack connection status for a user.
/// Returns null if no connection exists.
/// </summary>
internal record GetSlackConnectionStatusQuery(Guid UserId) : IQuery<SlackConnectionStatusDto?>;
