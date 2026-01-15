using System.Text.Json;
using Api.BoundedContexts.GameManagement.Domain.Entities;
using Api.BoundedContexts.GameManagement.Domain.Repositories;
using Api.Infrastructure;
using Api.Infrastructure.Entities.GameManagement;
using Api.SharedKernel.Application.Services;
using Api.SharedKernel.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.GameManagement.Infrastructure.Persistence;

/// <summary>
/// EF Core implementation of GameSessionState repository.
/// Maps between domain GameSessionState entity and GameSessionStateEntity persistence model.
/// Issue #2403: GameSessionState Entity
/// </summary>
internal class GameSessionStateRepository : RepositoryBase, IGameSessionStateRepository
{
    public GameSessionStateRepository(MeepleAiDbContext dbContext, IDomainEventCollector eventCollector)
        : base(dbContext, eventCollector)
    {
    }

    public async Task<GameSessionState?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var stateEntity = await DbContext.GameSessionStates
            .Include(s => s.Snapshots)
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == id, cancellationToken)
            .ConfigureAwait(false);

        return stateEntity != null ? MapToDomain(stateEntity) : null;
    }

    public async Task<GameSessionState?> GetBySessionIdAsync(Guid gameSessionId, CancellationToken cancellationToken = default)
    {
        var stateEntity = await DbContext.GameSessionStates
            .Include(s => s.Snapshots)
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.GameSessionId == gameSessionId, cancellationToken)
            .ConfigureAwait(false);

        return stateEntity != null ? MapToDomain(stateEntity) : null;
    }

    public async Task AddAsync(GameSessionState state, CancellationToken cancellationToken = default)
    {
        // Collect domain events BEFORE mapping to persistence entity
        CollectDomainEvents(state);

        var stateEntity = MapToPersistence(state);
        await DbContext.GameSessionStates.AddAsync(stateEntity, cancellationToken).ConfigureAwait(false);
    }

    public Task UpdateAsync(GameSessionState state, CancellationToken cancellationToken = default)
    {
        // Collect domain events BEFORE updating persistence entity
        CollectDomainEvents(state);

        var stateEntity = MapToPersistence(state);

        // Detach existing tracked entity to avoid conflicts
        var tracked = DbContext.ChangeTracker.Entries<GameSessionStateEntity>()
            .FirstOrDefault(e => e.Entity.Id == stateEntity.Id);

        if (tracked != null)
            tracked.State = EntityState.Detached;

        DbContext.GameSessionStates.Update(stateEntity);
        return Task.CompletedTask;
    }

    public async Task<bool> ExistsBySessionIdAsync(Guid gameSessionId, CancellationToken cancellationToken = default)
    {
        return await DbContext.GameSessionStates
            .AnyAsync(s => s.GameSessionId == gameSessionId, cancellationToken)
            .ConfigureAwait(false);
    }

    public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        // Check if already tracked to avoid EF Core tracking conflicts
        var tracked = DbContext.ChangeTracker.Entries<GameSessionStateEntity>()
            .FirstOrDefault(e => e.Entity.Id == id);

        if (tracked != null)
        {
            DbContext.GameSessionStates.Remove(tracked.Entity);
        }
        else
        {
            var stateEntity = new GameSessionStateEntity { Id = id };
            DbContext.GameSessionStates.Remove(stateEntity);
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Maps persistence entity to domain entity.
    /// </summary>
    private static GameSessionState MapToDomain(GameSessionStateEntity entity)
    {
        // Parse current state JSON
        var currentState = JsonDocument.Parse(entity.CurrentStateJson);

        // Map snapshots collection
        var snapshotsList = entity.Snapshots.Select(MapSnapshotToDomain).ToList();

        // Use internal constructor for reconstruction
        return new GameSessionState(
            id: entity.Id,
            gameSessionId: entity.GameSessionId,
            templateId: entity.TemplateId,
            currentState: currentState,
            version: entity.Version,
            lastUpdatedAt: entity.LastUpdatedAt,
            lastUpdatedBy: entity.LastUpdatedBy,
            snapshots: snapshotsList
        );
    }

    /// <summary>
    /// Maps domain entity to persistence entity.
    /// </summary>
    private static GameSessionStateEntity MapToPersistence(GameSessionState domainEntity)
    {
        var stateEntity = new GameSessionStateEntity
        {
            Id = domainEntity.Id,
            GameSessionId = domainEntity.GameSessionId,
            TemplateId = domainEntity.TemplateId,
            CurrentStateJson = domainEntity.GetStateAsString(),
            Version = domainEntity.Version,
            LastUpdatedAt = domainEntity.LastUpdatedAt,
            LastUpdatedBy = domainEntity.LastUpdatedBy
        };

        // Map snapshots
        stateEntity.Snapshots = domainEntity.Snapshots
            .Select(MapSnapshotToPersistence)
            .ToList();

        return stateEntity;
    }

    /// <summary>
    /// Maps snapshot persistence entity to domain entity.
    /// </summary>
    private static GameStateSnapshot MapSnapshotToDomain(GameStateSnapshotEntity entity)
    {
        var stateJson = JsonDocument.Parse(entity.StateJson);

        // Use internal constructor for reconstruction
        return new GameStateSnapshot(
            id: entity.Id,
            sessionStateId: entity.SessionStateId,
            state: stateJson,
            turnNumber: entity.TurnNumber,
            description: entity.Description,
            createdAt: entity.CreatedAt,
            createdBy: entity.CreatedBy
        );
    }

    /// <summary>
    /// Maps snapshot domain entity to persistence entity.
    /// </summary>
    private static GameStateSnapshotEntity MapSnapshotToPersistence(GameStateSnapshot domainEntity)
    {
        return new GameStateSnapshotEntity
        {
            Id = domainEntity.Id,
            SessionStateId = domainEntity.SessionStateId,
            StateJson = domainEntity.GetStateAsString(),
            TurnNumber = domainEntity.TurnNumber,
            Description = domainEntity.Description,
            CreatedAt = domainEntity.CreatedAt,
            CreatedBy = domainEntity.CreatedBy
        };
    }
}
