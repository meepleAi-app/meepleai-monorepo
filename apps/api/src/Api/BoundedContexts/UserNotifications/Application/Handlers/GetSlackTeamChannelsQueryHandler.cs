using System.Text.Json;
using Api.BoundedContexts.UserNotifications.Application.DTOs;
using Api.BoundedContexts.UserNotifications.Application.Queries;
using Api.Infrastructure;
using Api.Infrastructure.Entities.UserNotifications;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.UserNotifications.Application.Handlers;

/// <summary>
/// Handler for GetSlackTeamChannelsQuery.
/// Returns all Slack team channel configurations for admin management.
/// </summary>
internal class GetSlackTeamChannelsQueryHandler
    : IQueryHandler<GetSlackTeamChannelsQuery, IReadOnlyList<SlackTeamChannelDto>>
{
    private readonly MeepleAiDbContext _dbContext;

    public GetSlackTeamChannelsQueryHandler(MeepleAiDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<SlackTeamChannelDto>> Handle(
        GetSlackTeamChannelsQuery query, CancellationToken cancellationToken)
    {
        var entities = await _dbContext.Set<SlackTeamChannelConfigEntity>()
            .AsNoTracking()
            .OrderBy(e => e.ChannelName)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        return entities.Select(e =>
        {
            var types = JsonSerializer.Deserialize<string[]>(e.NotificationTypes) ?? [];
            return new SlackTeamChannelDto(
                e.Id, e.ChannelName, e.WebhookUrl, types, e.IsEnabled, e.OverridesDefault);
        }).ToList();
    }
}
