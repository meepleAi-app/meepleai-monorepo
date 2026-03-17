using Api.BoundedContexts.UserNotifications.Domain.Aggregates;
using Api.BoundedContexts.UserNotifications.Domain.Repositories;
using Api.Infrastructure;
using Api.Infrastructure.Entities.UserNotifications;
using Api.SharedKernel.Application.Services;
using Api.SharedKernel.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.UserNotifications.Infrastructure.Persistence;

/// <summary>
/// EF Core implementation of SlackConnection repository.
/// Maps between domain SlackConnection aggregate and SlackConnectionEntity persistence model.
/// </summary>
internal class SlackConnectionRepository : RepositoryBase, ISlackConnectionRepository
{
    public SlackConnectionRepository(MeepleAiDbContext dbContext, IDomainEventCollector eventCollector)
        : base(dbContext, eventCollector)
    {
    }

    public async Task AddAsync(SlackConnection connection, CancellationToken ct = default)
    {
        CollectDomainEvents(connection);
        var entity = MapToPersistence(connection);
        await DbContext.Set<SlackConnectionEntity>().AddAsync(entity, ct).ConfigureAwait(false);
    }

    public async Task UpdateAsync(SlackConnection connection, CancellationToken ct = default)
    {
        CollectDomainEvents(connection);
        var entity = MapToPersistence(connection);

        var tracked = DbContext.ChangeTracker.Entries<SlackConnectionEntity>()
            .FirstOrDefault(e => e.Entity.Id == entity.Id);

        if (tracked != null)
        {
            tracked.CurrentValues.SetValues(entity);
            tracked.State = EntityState.Modified;
        }
        else
        {
            DbContext.Set<SlackConnectionEntity>().Update(entity);
        }

        await Task.CompletedTask.ConfigureAwait(false);
    }

    public async Task<SlackConnection?> GetByUserIdAsync(Guid userId, CancellationToken ct = default)
    {
        var entity = await DbContext.Set<SlackConnectionEntity>()
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.UserId == userId, ct).ConfigureAwait(false);

        return entity != null ? MapToDomain(entity) : null;
    }

    public async Task<SlackConnection?> GetBySlackUserIdAsync(string slackUserId, CancellationToken ct = default)
    {
        var entity = await DbContext.Set<SlackConnectionEntity>()
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.SlackUserId == slackUserId, ct).ConfigureAwait(false);

        return entity != null ? MapToDomain(entity) : null;
    }

    public async Task<SlackConnection?> GetActiveByUserIdAsync(Guid userId, CancellationToken ct = default)
    {
        var entity = await DbContext.Set<SlackConnectionEntity>()
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.UserId == userId && e.IsActive, ct).ConfigureAwait(false);

        return entity != null ? MapToDomain(entity) : null;
    }

    public async Task<int> GetActiveConnectionCountAsync(CancellationToken ct = default)
    {
        return await DbContext.Set<SlackConnectionEntity>()
            .AsNoTracking()
            .CountAsync(e => e.IsActive, ct).ConfigureAwait(false);
    }

    private static SlackConnectionEntity MapToPersistence(SlackConnection connection)
    {
        return new SlackConnectionEntity
        {
            Id = connection.Id,
            UserId = connection.UserId,
            SlackUserId = connection.SlackUserId,
            SlackTeamId = connection.SlackTeamId,
            SlackTeamName = connection.SlackTeamName,
            BotAccessToken = connection.BotAccessToken,
            DmChannelId = connection.DmChannelId,
            IsActive = connection.IsActive,
            ConnectedAt = connection.ConnectedAt,
            DisconnectedAt = connection.DisconnectedAt
        };
    }

    private static SlackConnection MapToDomain(SlackConnectionEntity entity)
    {
        return SlackConnection.Reconstitute(
            id: entity.Id,
            userId: entity.UserId,
            slackUserId: entity.SlackUserId,
            slackTeamId: entity.SlackTeamId,
            slackTeamName: entity.SlackTeamName,
            botAccessToken: entity.BotAccessToken,
            dmChannelId: entity.DmChannelId,
            isActive: entity.IsActive,
            connectedAt: entity.ConnectedAt,
            disconnectedAt: entity.DisconnectedAt);
    }
}
