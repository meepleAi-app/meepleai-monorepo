using Api.BoundedContexts.SystemConfiguration.Domain.Entities;
using Api.BoundedContexts.SystemConfiguration.Domain.Repositories;
using Api.Infrastructure;
using Api.Infrastructure.Entities.SystemConfiguration;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.SystemConfiguration.Infrastructure.Persistence;

public sealed class EfAiModelConfigurationRepository : IAiModelConfigurationRepository
{
    private readonly MeepleAiDbContext _db;

    public EfAiModelConfigurationRepository(MeepleAiDbContext db) => _db = db;

    public async Task<AiModelConfiguration?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _db.AiModelConfigurations
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == id, cancellationToken)
            .ConfigureAwait(false);

        return entity != null ? MapToDomain(entity) : null;
    }

    public async Task<AiModelConfiguration?> GetByModelIdAsync(string modelId, CancellationToken cancellationToken = default)
    {
        var entity = await _db.AiModelConfigurations
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.ModelId == modelId, cancellationToken)
            .ConfigureAwait(false);

        return entity != null ? MapToDomain(entity) : null;
    }

    public async Task<IReadOnlyList<AiModelConfiguration>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var entities = await _db.AiModelConfigurations
            .AsNoTracking()
            .OrderBy(e => e.Priority)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return entities.Select(MapToDomain).ToList();
    }

    public async Task<IReadOnlyList<AiModelConfiguration>> GetActiveAsync(CancellationToken cancellationToken = default)
    {
        var entities = await _db.AiModelConfigurations
            .AsNoTracking()
            .Where(e => e.IsActive)
            .OrderBy(e => e.Priority)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return entities.Select(MapToDomain).ToList();
    }

    public async Task<AiModelConfiguration?> GetPrimaryAsync(CancellationToken cancellationToken = default)
    {
        var entity = await _db.AiModelConfigurations
            .AsNoTracking()
            .Where(e => e.IsPrimary && e.IsActive)
            .OrderBy(e => e.Priority)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        return entity != null ? MapToDomain(entity) : null;
    }

    public async Task<bool> AnyAsync(CancellationToken cancellationToken = default)
    {
        return await _db.AiModelConfigurations
            .AnyAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task AddAsync(AiModelConfiguration entity, CancellationToken cancellationToken = default)
    {
        var dbEntity = MapToDb(entity);
        await _db.AiModelConfigurations.AddAsync(dbEntity, cancellationToken).ConfigureAwait(false);
    }

    public async Task AddRangeAsync(IEnumerable<AiModelConfiguration> entities, CancellationToken cancellationToken = default)
    {
        var dbEntities = entities.Select(MapToDb);
        await _db.AiModelConfigurations.AddRangeAsync(dbEntities, cancellationToken).ConfigureAwait(false);
    }

    public Task UpdateAsync(AiModelConfiguration entity, CancellationToken cancellationToken = default)
    {
        var dbEntity = MapToDb(entity);
        _db.AiModelConfigurations.Update(dbEntity);
        return Task.CompletedTask;
    }

    public Task DeleteAsync(AiModelConfiguration entity, CancellationToken cancellationToken = default)
    {
        var dbEntity = MapToDb(entity);
        _db.AiModelConfigurations.Remove(dbEntity);
        return Task.CompletedTask;
    }

    private static AiModelConfiguration MapToDomain(AiModelConfigurationEntity entity)
    {
        var domainEntity = (AiModelConfiguration)Activator.CreateInstance(
            typeof(AiModelConfiguration),
            nonPublic: true)!;

        typeof(AiModelConfiguration).GetProperty(nameof(AiModelConfiguration.Id))!
            .SetValue(domainEntity, entity.Id);
        typeof(AiModelConfiguration).GetProperty(nameof(AiModelConfiguration.ModelId))!
            .SetValue(domainEntity, entity.ModelId);
        typeof(AiModelConfiguration).GetProperty(nameof(AiModelConfiguration.DisplayName))!
            .SetValue(domainEntity, entity.DisplayName);
        typeof(AiModelConfiguration).GetProperty(nameof(AiModelConfiguration.Provider))!
            .SetValue(domainEntity, entity.Provider);
        typeof(AiModelConfiguration).GetProperty(nameof(AiModelConfiguration.Priority))!
            .SetValue(domainEntity, entity.Priority);
        typeof(AiModelConfiguration).GetProperty(nameof(AiModelConfiguration.IsActive))!
            .SetValue(domainEntity, entity.IsActive);
        typeof(AiModelConfiguration).GetProperty(nameof(AiModelConfiguration.IsPrimary))!
            .SetValue(domainEntity, entity.IsPrimary);
        typeof(AiModelConfiguration).GetProperty(nameof(AiModelConfiguration.CreatedAt))!
            .SetValue(domainEntity, entity.CreatedAt);
        typeof(AiModelConfiguration).GetProperty(nameof(AiModelConfiguration.UpdatedAt))!
            .SetValue(domainEntity, entity.UpdatedAt);

        return domainEntity;
    }

    private static AiModelConfigurationEntity MapToDb(AiModelConfiguration domain)
    {
        return new AiModelConfigurationEntity
        {
            Id = domain.Id,
            ModelId = domain.ModelId,
            DisplayName = domain.DisplayName,
            Provider = domain.Provider,
            Priority = domain.Priority,
            IsActive = domain.IsActive,
            IsPrimary = domain.IsPrimary,
            CreatedAt = domain.CreatedAt,
            UpdatedAt = domain.UpdatedAt
        };
    }
}
