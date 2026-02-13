using Api.BoundedContexts.UserLibrary.Domain.Entities;
using Api.BoundedContexts.UserLibrary.Domain.Enums;
using Api.BoundedContexts.UserLibrary.Domain.Repositories;
using Api.Infrastructure;
using Api.Infrastructure.Entities.UserLibrary;
using Api.SharedKernel.Application.Services;
using Api.SharedKernel.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.UserLibrary.Infrastructure.Persistence;

/// <summary>
/// EF Core implementation of PrivateGame repository.
/// Maps between domain PrivateGame entity and PrivateGameEntity persistence model.
/// Issue #3662: Phase 1 - Data Model &amp; Core Infrastructure for Private Games.
/// </summary>
internal class PrivateGameRepository : RepositoryBase, IPrivateGameRepository
{
    public PrivateGameRepository(
        MeepleAiDbContext dbContext,
        IDomainEventCollector eventCollector)
        : base(dbContext, eventCollector)
    {
    }

    public async Task<PrivateGame?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await DbContext.PrivateGames
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == id, cancellationToken)
            .ConfigureAwait(false);

        return entity != null ? MapToDomain(entity) : null;
    }

    public async Task<IReadOnlyList<PrivateGame>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var entities = await DbContext.PrivateGames
            .AsNoTracking()
            .OrderByDescending(e => e.CreatedAt)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return entities.Select(MapToDomain).ToList();
    }

    public async Task<IReadOnlyList<PrivateGame>> GetByOwnerIdAsync(Guid ownerId, CancellationToken cancellationToken = default)
    {
        var entities = await DbContext.PrivateGames
            .AsNoTracking()
            .Where(e => e.OwnerId == ownerId)
            .OrderByDescending(e => e.CreatedAt)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return entities.Select(MapToDomain).ToList();
    }

    public async Task<PrivateGame?> GetByOwnerAndBggIdAsync(Guid ownerId, int bggId, CancellationToken cancellationToken = default)
    {
        var entity = await DbContext.PrivateGames
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.OwnerId == ownerId && e.BggId == bggId, cancellationToken)
            .ConfigureAwait(false);

        return entity != null ? MapToDomain(entity) : null;
    }

    public async Task<bool> ExistsByOwnerAndBggIdAsync(Guid ownerId, int bggId, CancellationToken cancellationToken = default)
    {
        return await DbContext.PrivateGames
            .AsNoTracking()
            .AnyAsync(e => e.OwnerId == ownerId && e.BggId == bggId, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<int> CountByOwnerIdAsync(Guid ownerId, CancellationToken cancellationToken = default)
    {
        return await DbContext.PrivateGames
            .AsNoTracking()
            .CountAsync(e => e.OwnerId == ownerId, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<PrivateGame>> SearchByTitleAsync(Guid ownerId, string searchTerm, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(searchTerm))
            return Array.Empty<PrivateGame>();

        var normalizedSearchTerm = searchTerm.ToLowerInvariant();

        var entities = await DbContext.PrivateGames
            .AsNoTracking()
            .Where(e => e.OwnerId == ownerId)
            .Where(e => EF.Functions.ILike(e.Title, $"%{normalizedSearchTerm}%"))
            .OrderBy(e => e.Title)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return entities.Select(MapToDomain).ToList();
    }

    public async Task<IReadOnlyList<PrivateGame>> GetByOwnerIdWithDeletedAsync(Guid ownerId, CancellationToken cancellationToken = default)
    {
        var entities = await DbContext.PrivateGames
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Where(e => e.OwnerId == ownerId)
            .OrderByDescending(e => e.CreatedAt)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return entities.Select(MapToDomain).ToList();
    }

    public async Task AddAsync(PrivateGame entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);
        CollectDomainEvents(entity);

        var persistenceEntity = MapToPersistence(entity);
        await DbContext.PrivateGames.AddAsync(persistenceEntity, cancellationToken).ConfigureAwait(false);
    }

    public Task UpdateAsync(PrivateGame entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);
        CollectDomainEvents(entity);

        var persistenceEntity = MapToPersistence(entity);

        // Issue #3531: Check if entity is already tracked to avoid InvalidOperationException
        var existingEntry = DbContext.ChangeTracker.Entries<PrivateGameEntity>()
            .FirstOrDefault(e => e.Entity.Id == persistenceEntity.Id);

        if (existingEntry != null)
        {
            existingEntry.CurrentValues.SetValues(persistenceEntity);
        }
        else
        {
            DbContext.PrivateGames.Update(persistenceEntity);
        }

        return Task.CompletedTask;
    }

    public Task DeleteAsync(PrivateGame entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);
        CollectDomainEvents(entity);

        var persistenceEntity = MapToPersistence(entity);
        DbContext.PrivateGames.Remove(persistenceEntity);
        return Task.CompletedTask;
    }

    public async Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await DbContext.PrivateGames
            .AsNoTracking()
            .AnyAsync(e => e.Id == id, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Maps persistence entity to domain entity.
    /// </summary>
    private static PrivateGame MapToDomain(PrivateGameEntity entity)
    {
        PrivateGame domainEntity;

        if (entity.Source == PrivateGameSource.BoardGameGeek && entity.BggId.HasValue)
        {
            domainEntity = PrivateGame.CreateFromBgg(
                ownerId: entity.OwnerId,
                bggId: entity.BggId.Value,
                title: entity.Title,
                yearPublished: entity.YearPublished,
                description: entity.Description,
                minPlayers: entity.MinPlayers,
                maxPlayers: entity.MaxPlayers,
                playingTimeMinutes: entity.PlayingTimeMinutes,
                minAge: entity.MinAge,
                complexityRating: entity.ComplexityRating,
                imageUrl: entity.ImageUrl,
                thumbnailUrl: entity.ThumbnailUrl
            );
        }
        else
        {
            domainEntity = PrivateGame.CreateManual(
                ownerId: entity.OwnerId,
                title: entity.Title,
                minPlayers: entity.MinPlayers,
                maxPlayers: entity.MaxPlayers,
                yearPublished: entity.YearPublished,
                description: entity.Description,
                playingTimeMinutes: entity.PlayingTimeMinutes,
                minAge: entity.MinAge,
                complexityRating: entity.ComplexityRating,
                imageUrl: entity.ImageUrl
            );
        }

        // Use reflection to set properties from persistence
        var type = typeof(PrivateGame);

#pragma warning disable S3011 // Reflection needed for domain reconstruction from persistence
        type.GetProperty("Id")?.GetSetMethod(true)?.Invoke(domainEntity, new object[] { entity.Id });

        if (entity.ThumbnailUrl != null)
            type.GetProperty("ThumbnailUrl")?.GetSetMethod(true)?.Invoke(domainEntity, new object[] { entity.ThumbnailUrl });

        type.GetProperty("CreatedAt")?.GetSetMethod(true)?.Invoke(domainEntity, new object[] { entity.CreatedAt });

        if (entity.UpdatedAt.HasValue)
            type.GetProperty("UpdatedAt")?.GetSetMethod(true)?.Invoke(domainEntity, new object[] { entity.UpdatedAt });

        if (entity.BggSyncedAt.HasValue)
            type.GetProperty("BggSyncedAt")?.GetSetMethod(true)?.Invoke(domainEntity, new object[] { entity.BggSyncedAt });

        type.GetProperty("IsDeleted")?.GetSetMethod(true)?.Invoke(domainEntity, new object[] { entity.IsDeleted });

        if (entity.DeletedAt.HasValue)
            type.GetProperty("DeletedAt")?.GetSetMethod(true)?.Invoke(domainEntity, new object[] { entity.DeletedAt });

        // Issue #4228: Map AgentDefinitionId
        if (entity.AgentDefinitionId.HasValue)
            type.GetProperty("AgentDefinitionId")?.GetSetMethod(true)?.Invoke(domainEntity, new object[] { entity.AgentDefinitionId });
#pragma warning restore S3011

        // Clear domain events raised during construction
        domainEntity.ClearDomainEvents();

        return domainEntity;
    }

    /// <summary>
    /// Maps domain entity to persistence entity.
    /// </summary>
    private static PrivateGameEntity MapToPersistence(PrivateGame domainEntity)
    {
        ArgumentNullException.ThrowIfNull(domainEntity);

        return new PrivateGameEntity
        {
            Id = domainEntity.Id,
            OwnerId = domainEntity.OwnerId,
            BggId = domainEntity.BggId,
            Title = domainEntity.Title,
            YearPublished = domainEntity.YearPublished,
            Description = domainEntity.Description,
            MinPlayers = domainEntity.MinPlayers,
            MaxPlayers = domainEntity.MaxPlayers,
            PlayingTimeMinutes = domainEntity.PlayingTimeMinutes,
            MinAge = domainEntity.MinAge,
            ComplexityRating = domainEntity.ComplexityRating,
            ImageUrl = domainEntity.ImageUrl,
            ThumbnailUrl = domainEntity.ThumbnailUrl,
            Source = domainEntity.Source,
            BggSyncedAt = domainEntity.BggSyncedAt,
            CreatedAt = domainEntity.CreatedAt,
            UpdatedAt = domainEntity.UpdatedAt,
            IsDeleted = domainEntity.IsDeleted,
            DeletedAt = domainEntity.DeletedAt,
            AgentDefinitionId = domainEntity.AgentDefinitionId  // Issue #4228
        };
    }
}
