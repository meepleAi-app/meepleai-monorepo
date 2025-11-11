using Api.BoundedContexts.SystemConfiguration.Domain.Repositories;
using Api.BoundedContexts.SystemConfiguration.Domain.ValueObjects;
using Api.Infrastructure;
using Microsoft.EntityFrameworkCore;
using SystemConfigurationAggregate = Api.BoundedContexts.SystemConfiguration.Domain.Entities.SystemConfiguration;

namespace Api.BoundedContexts.SystemConfiguration.Infrastructure.Persistence;

public class ConfigurationRepository : IConfigurationRepository
{
    private readonly MeepleAiDbContext _dbContext;

    public ConfigurationRepository(MeepleAiDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<SystemConfigurationAggregate?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _dbContext.Set<Api.Infrastructure.Entities.SystemConfigurationEntity>()
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

        return entity != null ? MapToDomain(entity) : null;
    }

    public async Task<List<SystemConfigurationAggregate>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var entities = await _dbContext.Set<Api.Infrastructure.Entities.SystemConfigurationEntity>()
            .AsNoTracking()
            .OrderBy(c => c.Key)
            .ToListAsync(cancellationToken);

        return entities.Select(MapToDomain).ToList();
    }

    public async Task<SystemConfigurationAggregate?> GetByKeyAsync(string key, CancellationToken cancellationToken = default)
    {
        var entity = await _dbContext.Set<Api.Infrastructure.Entities.SystemConfigurationEntity>()
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Key == key, cancellationToken);

        return entity != null ? MapToDomain(entity) : null;
    }

    public async Task<IReadOnlyList<SystemConfigurationAggregate>> GetByCategoryAsync(string category, CancellationToken cancellationToken = default)
    {
        var entities = await _dbContext.Set<Api.Infrastructure.Entities.SystemConfigurationEntity>()
            .AsNoTracking()
            .Where(c => c.Category == category)
            .OrderBy(c => c.Key)
            .ToListAsync(cancellationToken);

        return entities.Select(MapToDomain).ToList();
    }

    public async Task<IReadOnlyList<SystemConfigurationAggregate>> GetActiveConfigurationsAsync(CancellationToken cancellationToken = default)
    {
        var entities = await _dbContext.Set<Api.Infrastructure.Entities.SystemConfigurationEntity>()
            .AsNoTracking()
            .Where(c => c.IsActive)
            .OrderBy(c => c.Key)
            .ToListAsync(cancellationToken);

        return entities.Select(MapToDomain).ToList();
    }

    public async Task AddAsync(SystemConfigurationAggregate config, CancellationToken cancellationToken = default)
    {
        var entity = MapToPersistence(config);
        await _dbContext.Set<Api.Infrastructure.Entities.SystemConfigurationEntity>().AddAsync(entity, cancellationToken);
    }

    public Task UpdateAsync(SystemConfigurationAggregate config, CancellationToken cancellationToken = default)
    {
        var entity = MapToPersistence(config);
        _dbContext.Set<Api.Infrastructure.Entities.SystemConfigurationEntity>().Update(entity);
        return Task.CompletedTask;
    }

    public Task DeleteAsync(SystemConfigurationAggregate config, CancellationToken cancellationToken = default)
    {
        var entity = MapToPersistence(config);
        _dbContext.Set<Api.Infrastructure.Entities.SystemConfigurationEntity>().Remove(entity);
        return Task.CompletedTask;
    }

    public async Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Set<Api.Infrastructure.Entities.SystemConfigurationEntity>()
            .AnyAsync(c => c.Id == id, cancellationToken);
    }

    private static SystemConfigurationAggregate MapToDomain(Api.Infrastructure.Entities.SystemConfigurationEntity entity)
    {
        var key = new ConfigKey(entity.Key);

        var config = new SystemConfigurationAggregate(
            id: entity.Id,
            key: key,
            value: entity.Value,
            valueType: entity.ValueType,
            createdByUserId: entity.CreatedByUserId,
            description: entity.Description,
            category: entity.Category,
            environment: entity.Environment,
            requiresRestart: entity.RequiresRestart
        );

        // Override properties from DB via reflection
        var isActiveProp = typeof(SystemConfigurationAggregate).GetProperty("IsActive");
        isActiveProp?.SetValue(config, entity.IsActive);

        var versionProp = typeof(SystemConfigurationAggregate).GetProperty("Version");
        versionProp?.SetValue(config, entity.Version);

        var previousValueProp = typeof(SystemConfigurationAggregate).GetProperty("PreviousValue");
        previousValueProp?.SetValue(config, entity.PreviousValue);

        var createdAtProp = typeof(SystemConfigurationAggregate).GetProperty("CreatedAt");
        createdAtProp?.SetValue(config, entity.CreatedAt);

        var updatedAtProp = typeof(SystemConfigurationAggregate).GetProperty("UpdatedAt");
        updatedAtProp?.SetValue(config, entity.UpdatedAt);

        var updatedByUserIdProp = typeof(SystemConfigurationAggregate).GetProperty("UpdatedByUserId");
        updatedByUserIdProp?.SetValue(config, entity.UpdatedByUserId);

        var lastToggledAtProp = typeof(SystemConfigurationAggregate).GetProperty("LastToggledAt");
        lastToggledAtProp?.SetValue(config, entity.LastToggledAt);

        return config;
    }

    private static Api.Infrastructure.Entities.SystemConfigurationEntity MapToPersistence(SystemConfigurationAggregate domain)
    {
        return new Api.Infrastructure.Entities.SystemConfigurationEntity
        {
            Id = domain.Id,
            Key = domain.Key.Value,
            Value = domain.Value,
            ValueType = domain.ValueType,
            Description = domain.Description,
            Category = domain.Category,
            IsActive = domain.IsActive,
            RequiresRestart = domain.RequiresRestart,
            Environment = domain.Environment,
            Version = domain.Version,
            PreviousValue = domain.PreviousValue,
            CreatedAt = domain.CreatedAt,
            UpdatedAt = domain.UpdatedAt,
            CreatedByUserId = domain.CreatedByUserId,
            UpdatedByUserId = domain.UpdatedByUserId,
            LastToggledAt = domain.LastToggledAt
        };
    }
}
