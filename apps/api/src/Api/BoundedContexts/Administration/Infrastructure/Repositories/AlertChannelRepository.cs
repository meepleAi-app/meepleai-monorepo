using Api.BoundedContexts.Administration.Domain.Aggregates.AlertChannels;
using Api.BoundedContexts.Administration.Domain.Repositories;
using Api.Infrastructure;
using Api.Infrastructure.Entities.Administration;
using Api.SharedKernel.Application.Services;
using Api.SharedKernel.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.Administration.Infrastructure.Repositories;

/// <summary>
/// EF-Core backed implementation of <see cref="IAlertChannelRepository"/>
/// (Issue #1840 SP5 F4-C7).
/// </summary>
internal sealed class AlertChannelRepository : RepositoryBase, IAlertChannelRepository
{
    public AlertChannelRepository(MeepleAiDbContext dbContext, IDomainEventCollector eventCollector)
        : base(dbContext, eventCollector)
    {
    }

    public async Task<IReadOnlyList<AlertChannel>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var entities = await DbContext.AlertChannels
            .AsNoTracking()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return entities.Select(MapToDomain).ToList();
    }

    public async Task<AlertChannel?> GetByTypeAsync(AlertChannelType type, CancellationToken cancellationToken = default)
    {
        var key = type.ToWireValue();
        var entity = await DbContext.AlertChannels
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Type == key, cancellationToken)
            .ConfigureAwait(false);

        return entity is null ? null : MapToDomain(entity);
    }

    public async Task UpsertAsync(AlertChannel channel, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(channel);

        var key = channel.Type.ToWireValue();

        // Issue #3866: `.AsTracking()` is REQUIRED and the comment that used to sit here — "we
        // deliberately re-query without AsNoTracking so EF can attach the row" — was built on a
        // false premise. The DbContext default IS NoTracking (PERF-06,
        // InfrastructureServiceExtensions.cs), so omitting AsNoTracking() buys nothing: the row came
        // back DETACHED, the in-place assignments below reached no change tracker, and
        // SaveChangesAsync wrote nothing and raised nothing. Every edit was silently discarded.
        var tracked = await DbContext.AlertChannels
            .AsTracking()
            .FirstOrDefaultAsync(c => c.Type == key, cancellationToken)
            .ConfigureAwait(false);

        if (tracked is null)
        {
            var entity = new AlertChannelEntity
            {
                Type = key,
                ConfigJson = channel.ConfigJson,
                IsEnabled = channel.IsEnabled,
                LastTestedAt = channel.LastTestedAt,
                LastTestStatus = channel.LastTestStatus,
                LastTestMessage = channel.LastTestMessage,
                CreatedAt = channel.CreatedAt,
                UpdatedAt = channel.UpdatedAt,
                CreatedBy = channel.CreatedBy,
                UpdatedBy = channel.UpdatedBy,
                LastDispatchedEventId = channel.LastDispatchedEventId,
            };
            await DbContext.AlertChannels.AddAsync(entity, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            // Force EF to check the concurrency token: assigning the original
            // xmin via Entry.OriginalValues makes DbUpdateConcurrencyException
            // fire when another admin has bumped xmin since the aggregate was loaded.
            DbContext.Entry(tracked).Property(p => p.Xmin).OriginalValue = channel.Xmin;

            tracked.ConfigJson = channel.ConfigJson;
            tracked.IsEnabled = channel.IsEnabled;
            tracked.LastTestedAt = channel.LastTestedAt;
            tracked.LastTestStatus = channel.LastTestStatus;
            tracked.LastTestMessage = channel.LastTestMessage;
            tracked.UpdatedAt = channel.UpdatedAt;
            tracked.UpdatedBy = channel.UpdatedBy;
            tracked.LastDispatchedEventId = channel.LastDispatchedEventId;
        }

        await DbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private static AlertChannel MapToDomain(AlertChannelEntity e) =>
        AlertChannel.Reconstitute(
            AlertChannelTypeExtensions.FromWireValue(e.Type),
            e.ConfigJson,
            e.IsEnabled,
            e.LastTestedAt,
            e.LastTestStatus,
            e.LastTestMessage,
            e.CreatedAt,
            e.UpdatedAt,
            e.CreatedBy,
            e.UpdatedBy,
            e.Xmin,
            e.LastDispatchedEventId);
}
