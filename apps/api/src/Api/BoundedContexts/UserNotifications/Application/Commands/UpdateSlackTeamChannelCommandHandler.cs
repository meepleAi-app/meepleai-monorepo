using System.Text.Json;
using Api.BoundedContexts.UserNotifications.Application.Commands;
using Api.Infrastructure;
using Api.Infrastructure.Entities.UserNotifications;
using Api.SharedKernel.Application.Interfaces;
using Api.Middleware.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.UserNotifications.Application.Commands;

/// <summary>
/// Handler for UpdateSlackTeamChannelCommand.
/// Updates a Slack team channel configuration with partial update semantics.
/// </summary>
internal class UpdateSlackTeamChannelCommandHandler : ICommandHandler<UpdateSlackTeamChannelCommand>
{
    private readonly MeepleAiDbContext _dbContext;

    public UpdateSlackTeamChannelCommandHandler(MeepleAiDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task Handle(UpdateSlackTeamChannelCommand command, CancellationToken cancellationToken)
    {
        var entity = await _dbContext.Set<SlackTeamChannelConfigEntity>()
            .FirstOrDefaultAsync(e => e.Id == command.Id, cancellationToken)
            .ConfigureAwait(false);

        if (entity is null)
            throw new NotFoundException("SlackTeamChannelConfig", command.Id.ToString());

        if (command.ChannelName is not null)
            entity.ChannelName = command.ChannelName;

        if (command.WebhookUrl is not null)
            entity.WebhookUrl = command.WebhookUrl;

        if (command.NotificationTypes is not null)
            entity.NotificationTypes = JsonSerializer.Serialize(command.NotificationTypes);

        if (command.IsEnabled.HasValue)
            entity.IsEnabled = command.IsEnabled.Value;

        if (command.OverridesDefault.HasValue)
            entity.OverridesDefault = command.OverridesDefault.Value;

        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
