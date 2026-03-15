using Api.BoundedContexts.UserNotifications.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.UserNotifications.Application.Queries;

/// <summary>
/// Query to retrieve all Slack team channel configurations for admin management.
/// </summary>
internal record GetSlackTeamChannelsQuery() : IQuery<IReadOnlyList<SlackTeamChannelDto>>;
