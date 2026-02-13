using Api.BoundedContexts.UserLibrary.Domain.Entities;
using Api.BoundedContexts.UserLibrary.Domain.Enums;
using Api.BoundedContexts.UserLibrary.Domain.Repositories;
using Api.BoundedContexts.UserLibrary.Domain.ValueObjects;
using Api.Infrastructure;
using Api.Infrastructure.Entities.UserLibrary;
using Api.SharedKernel.Application.Services;
using Api.SharedKernel.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.UserLibrary.Infrastructure.Persistence;

/// <summary>
/// EF Core implementation of UserCollectionEntry repository.
/// Maps between domain UserCollectionEntry entity and UserCollectionEntryEntity persistence model.
/// Issue #4263: Phase 2 - Generic UserCollection System
/// </summary>
internal class UserCollectionRepository : RepositoryBase, IUserCollectionRepository
{
    private readonly ILogger<UserCollectionRepository> _logger;

    public UserCollectionRepository(
        MeepleAiDbContext dbContext,
        IDomainEventCollector eventCollector,
        ILogger<UserCollectionRepository> logger)
        : base(dbContext, eventCollector)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<UserCollectionEntry?> GetByUserAndEntityAsync(
        Guid userId,
        EntityType entityType,
        Guid entityId,
        CancellationToken cancellationToken = default)
    {
        var entityTypeString = entityType.ToString();
        var entity = await DbContext.UserCollectionEntries
            .AsNoTracking()
            .FirstOrDefaultAsync(
                e => e.UserId == userId && e.EntityType == entityTypeString && e.EntityId == entityId,
                cancellationToken)
            .ConfigureAwait(false);

        return entity != null ? MapToDomain(entity) : null;
    }

    public async Task<IReadOnlyList<UserCollectionEntry>> GetByUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var entities = await DbContext.UserCollectionEntries
            .AsNoTracking()
            .Where(e => e.UserId == userId)
            .OrderByDescending(e => e.AddedAt)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return entities.Select(MapToDomain).ToList();
    }

    public async Task<IReadOnlyList<UserCollectionEntry>> GetByUserAndTypeAsync(
        Guid userId,
        EntityType entityType,
        CancellationToken cancellationToken = default)
    {
        var entityTypeString = entityType.ToString();
        var entities = await DbContext.UserCollectionEntries
            .AsNoTracking()
            .Where(e => e.UserId == userId && e.EntityType == entityTypeString)
            .OrderByDescending(e => e.AddedAt)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return entities.Select(MapToDomain).ToList();
    }

    public async Task<int> CountByUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await DbContext.UserCollectionEntries
            .AsNoTracking()
            .CountAsync(e => e.UserId == userId, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task AddAsync(UserCollectionEntry entry, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entry);
        CollectDomainEvents(entry);

        var entity = MapToEntity(entry);
        await DbContext.UserCollectionEntries.AddAsync(entity, cancellationToken).ConfigureAwait(false);
    }

    public Task UpdateAsync(UserCollectionEntry entry, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entry);
        CollectDomainEvents(entry);

        var entity = MapToEntity(entry);
        DbContext.UserCollectionEntries.Update(entity);
        return Task.CompletedTask;
    }

    public Task DeleteAsync(UserCollectionEntry entry, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entry);
        CollectDomainEvents(entry);

        var entity = MapToEntity(entry);
        DbContext.UserCollectionEntries.Remove(entity);
        return Task.CompletedTask;
    }

    public async Task<bool> ExistsAsync(
        Guid userId,
        EntityType entityType,
        Guid entityId,
        CancellationToken cancellationToken = default)
    {
        var entityTypeString = entityType.ToString();
        return await DbContext.UserCollectionEntries
            .AsNoTracking()
            .AnyAsync(e => e.UserId == userId && e.EntityType == entityTypeString && e.EntityId == entityId,
                cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Maps persistence entity to domain entity.
    /// </summary>
    private UserCollectionEntry MapToDomain(UserCollectionEntryEntity entity)
    {
        // Parse EntityType from string
        if (!Enum.TryParse<EntityType>(entity.EntityType, out var entityType))
        {
            _logger.LogWarning(
                "Invalid EntityType '{EntityType}' for UserCollectionEntry {EntryId}. Defaulting to Player.",
                entity.EntityType, entity.Id);
            entityType = EntityType.Player;
        }

        var entry = new UserCollectionEntry(entity.Id, entity.UserId, entityType, entity.EntityId);

        // Set notes if present
        if (!string.IsNullOrWhiteSpace(entity.Notes))
        {
            entry.UpdateNotes(entity.Notes);
        }

        // Set favorite status
        if (entity.IsFavorite)
        {
            entry.MarkAsFavorite();
        }

        // Deserialize metadata from JSONB
        if (!string.IsNullOrWhiteSpace(entity.MetadataJson))
        {
            try
            {
                var metadata = CollectionMetadata.FromJson(entity.MetadataJson);
                entry.UpdateMetadata(metadata);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "Failed to deserialize MetadataJson for UserCollectionEntry {EntryId}. JSON: {Json}",
                    entity.Id, entity.MetadataJson);
                // Entry will have empty metadata (graceful degradation)
            }
        }

        // Override AddedAt from DB using reflection (pattern from UserLibraryRepository)
        var addedAtProp = typeof(UserCollectionEntry).GetProperty("AddedAt");
        addedAtProp?.SetValue(entry, entity.AddedAt);

        // Clear domain events that were raised during construction
        // (we don't want to re-raise events for existing entities)
        entry.ClearDomainEvents();

        return entry;
    }

    /// <summary>
    /// Maps domain entity to persistence entity.
    /// </summary>
    private static UserCollectionEntryEntity MapToEntity(UserCollectionEntry domainEntity)
    {
        ArgumentNullException.ThrowIfNull(domainEntity);

        return new UserCollectionEntryEntity
        {
            Id = domainEntity.Id,
            UserId = domainEntity.UserId,
            EntityType = domainEntity.EntityType.ToString(),
            EntityId = domainEntity.EntityId,
            IsFavorite = domainEntity.IsFavorite,
            Notes = domainEntity.Notes,
            MetadataJson = domainEntity.Metadata?.ToJson(),
            AddedAt = domainEntity.AddedAt,
            CreatedAt = domainEntity.AddedAt,
            UpdatedAt = null
        };
    }
}
