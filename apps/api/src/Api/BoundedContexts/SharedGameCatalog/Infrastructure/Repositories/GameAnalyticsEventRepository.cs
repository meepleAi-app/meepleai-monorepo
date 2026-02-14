using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;
using Api.BoundedContexts.SharedGameCatalog.Domain.Enums;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.Infrastructure;
using Api.Infrastructure.Entities.SharedGameCatalog;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.SharedGameCatalog.Infrastructure.Repositories;

/// <summary>
/// Repository implementation for GameAnalyticsEvent entity.
/// Issue #3918: Catalog Trending Analytics Service
/// </summary>
internal sealed class GameAnalyticsEventRepository : IGameAnalyticsEventRepository
{
    private readonly MeepleAiDbContext _context;

    public GameAnalyticsEventRepository(MeepleAiDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task AddAsync(GameAnalyticsEvent analyticsEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(analyticsEvent);

        var entity = MapToEntity(analyticsEvent);
        _context.Set<GameAnalyticsEventEntity>().Add(entity);
        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<List<GameAnalyticsEvent>> GetEventsSinceAsync(DateTime since, CancellationToken cancellationToken = default)
    {
        var entities = await _context.Set<GameAnalyticsEventEntity>()
            .AsNoTracking()
            .Where(e => e.Timestamp >= since)
            .OrderByDescending(e => e.Timestamp)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return entities.Select(MapToDomain).ToList();
    }

    #region Mapping Methods

    private static GameAnalyticsEventEntity MapToEntity(GameAnalyticsEvent domainEvent)
    {
        return new GameAnalyticsEventEntity
        {
            Id = domainEvent.Id,
            GameId = domainEvent.GameId,
            EventType = (int)domainEvent.EventType,
            UserId = domainEvent.UserId,
            Timestamp = domainEvent.Timestamp
        };
    }

    private static GameAnalyticsEvent MapToDomain(GameAnalyticsEventEntity entity)
    {
        return new GameAnalyticsEvent(
            entity.Id,
            entity.GameId,
            (GameEventType)entity.EventType,
            entity.UserId,
            entity.Timestamp);
    }

    #endregion
}
