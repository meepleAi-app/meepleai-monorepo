using Api.BoundedContexts.UserLibrary.Domain.Entities;
using Api.BoundedContexts.UserLibrary.Domain.Repositories;
using Api.Infrastructure;
using Api.SharedKernel.Application.Services;
using Api.SharedKernel.Infrastructure;
using Api.Infrastructure.Entities.UserLibrary;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.UserLibrary.Infrastructure.Persistence;

/// <summary>
/// EF Core implementation of GameLabel repository.
/// </summary>
internal class GameLabelRepository : RepositoryBase, IGameLabelRepository
{

    public GameLabelRepository(MeepleAiDbContext dbContext, IDomainEventCollector eventCollector)
        : base(dbContext, eventCollector)
    {
    }

    public async Task<GameLabel?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await DbContext.GameLabels
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == id, cancellationToken).ConfigureAwait(false);

        return entity != null ? MapToDomain(entity) : null;
    }

    public async Task<IReadOnlyList<GameLabel>> GetPredefinedLabelsAsync(CancellationToken cancellationToken = default)
    {
        var entities = await DbContext.GameLabels
            .AsNoTracking()
            .Where(e => e.IsPredefined)
            .OrderBy(e => e.Name)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        return entities.Select(MapToDomain).ToList();
    }

    public async Task<IReadOnlyList<GameLabel>> GetUserLabelsAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var entities = await DbContext.GameLabels
            .AsNoTracking()
            .Where(e => !e.IsPredefined && e.UserId == userId)
            .OrderBy(e => e.Name)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        return entities.Select(MapToDomain).ToList();
    }

    public async Task<IReadOnlyList<GameLabel>> GetAvailableLabelsAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var entities = await DbContext.GameLabels
            .AsNoTracking()
            .Where(e => e.IsPredefined || e.UserId == userId)
            .OrderBy(e => e.IsPredefined ? 0 : 1) // Predefined first
            .ThenBy(e => e.Name)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        return entities.Select(MapToDomain).ToList();
    }

    public async Task<IReadOnlyList<GameLabel>> GetLabelsForEntryAsync(Guid userLibraryEntryId, CancellationToken cancellationToken = default)
    {
        var entities = await DbContext.UserGameLabels
            .AsNoTracking()
            .Where(ugl => ugl.UserLibraryEntryId == userLibraryEntryId)
            .Include(ugl => ugl.Label)
            .Select(ugl => ugl.Label!)
            .OrderBy(l => l.IsPredefined ? 0 : 1)
            .ThenBy(l => l.Name)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        return entities.Select(MapToDomain).ToList();
    }

    public async Task<bool> LabelNameExistsAsync(Guid userId, string name, CancellationToken cancellationToken = default)
    {
        var normalizedName = name.Trim();

        return await DbContext.GameLabels
            .AsNoTracking()
            .AnyAsync(e =>
                (e.IsPredefined || e.UserId == userId) &&
                EF.Functions.ILike(e.Name, normalizedName), cancellationToken).ConfigureAwait(false);
    }

    public async Task<GameLabel?> GetAccessibleLabelAsync(Guid userId, Guid labelId, CancellationToken cancellationToken = default)
    {
        var entity = await DbContext.GameLabels
            .AsNoTracking()
            .FirstOrDefaultAsync(e =>
                e.Id == labelId &&
                (e.IsPredefined || e.UserId == userId), cancellationToken).ConfigureAwait(false);

        return entity != null ? MapToDomain(entity) : null;
    }

    public async Task AddAsync(GameLabel label, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(label);

        var entity = MapToPersistence(label);
        await DbContext.GameLabels.AddAsync(entity, cancellationToken).ConfigureAwait(false);
    }

    public Task DeleteAsync(GameLabel label, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(label);

        var entity = MapToPersistence(label);
        DbContext.GameLabels.Remove(entity);
        return Task.CompletedTask;
    }

    private static GameLabel MapToDomain(GameLabelEntity entity)
    {
        // Use reflection to reconstruct domain entity
        if (entity.IsPredefined)
        {
            return GameLabel.CreatePredefinedWithId(entity.Id, entity.Name, entity.Color);
        }

        var label = GameLabel.CreateCustom(entity.UserId!.Value, entity.Name, entity.Color);

        // Override Id using reflection
        var idProp = typeof(GameLabel).BaseType?.GetProperty("Id");
        idProp?.SetValue(label, entity.Id);

        // Override CreatedAt using reflection
        var createdAtProp = typeof(GameLabel).GetProperty("CreatedAt");
        createdAtProp?.SetValue(label, entity.CreatedAt);

        return label;
    }

    private static GameLabelEntity MapToPersistence(GameLabel domainEntity)
    {
        return new GameLabelEntity
        {
            Id = domainEntity.Id,
            Name = domainEntity.Name,
            Color = domainEntity.Color,
            IsPredefined = domainEntity.IsPredefined,
            UserId = domainEntity.UserId,
            CreatedAt = domainEntity.CreatedAt
        };
    }
}
