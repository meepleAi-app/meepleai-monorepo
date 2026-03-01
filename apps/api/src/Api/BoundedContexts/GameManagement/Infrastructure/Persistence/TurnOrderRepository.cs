using System.Text.Json;
using Api.BoundedContexts.GameManagement.Domain.Entities.TurnOrder;
using Api.Infrastructure;
using Api.Infrastructure.Entities.GameManagement;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.GameManagement.Infrastructure.Persistence;

/// <summary>
/// EF Core implementation of the TurnOrder repository.
/// Maps between the domain TurnOrder entity and TurnOrderEntity persistence model.
/// Issue #4970: TurnOrder Entity + Endpoints + SSE.
/// </summary>
internal class TurnOrderRepository : ITurnOrderRepository
{
    private readonly MeepleAiDbContext _dbContext;

    public TurnOrderRepository(MeepleAiDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    public async Task<TurnOrder?> GetBySessionIdAsync(Guid sessionId, CancellationToken cancellationToken = default)
    {
        var entity = await _dbContext.TurnOrders
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.SessionId == sessionId, cancellationToken)
            .ConfigureAwait(false);

        return entity != null ? MapToDomain(entity) : null;
    }

    public async Task AddAsync(TurnOrder turnOrder, CancellationToken cancellationToken = default)
    {
        var entity = MapToPersistence(turnOrder);
        await _dbContext.TurnOrders.AddAsync(entity, cancellationToken).ConfigureAwait(false);
    }

    public Task UpdateAsync(TurnOrder turnOrder, CancellationToken cancellationToken = default)
    {
        var entity = MapToPersistence(turnOrder);

        var tracked = _dbContext.ChangeTracker.Entries<TurnOrderEntity>()
            .FirstOrDefault(e => e.Entity.Id == entity.Id);

        if (tracked != null)
            tracked.State = EntityState.Detached;

        _dbContext.TurnOrders.Update(entity);
        return Task.CompletedTask;
    }

    private static TurnOrder MapToDomain(TurnOrderEntity entity)
    {
        var playerOrder = JsonSerializer.Deserialize<List<string>>(entity.PlayerOrderJson)
            ?? new List<string>();

        return TurnOrder.Restore(
            id: entity.Id,
            sessionId: entity.SessionId,
            playerOrder: playerOrder,
            currentIndex: entity.CurrentIndex,
            roundNumber: entity.RoundNumber,
            createdAt: entity.CreatedAt,
            updatedAt: entity.UpdatedAt);
    }

    private static TurnOrderEntity MapToPersistence(TurnOrder turnOrder)
    {
        return new TurnOrderEntity
        {
            Id = turnOrder.Id,
            SessionId = turnOrder.SessionId,
            PlayerOrderJson = JsonSerializer.Serialize(turnOrder.PlayerOrder),
            CurrentIndex = turnOrder.CurrentIndex,
            RoundNumber = turnOrder.RoundNumber,
            CreatedAt = turnOrder.CreatedAt,
            UpdatedAt = turnOrder.UpdatedAt
        };
    }
}
