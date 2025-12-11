using Api.BoundedContexts.SystemConfiguration.Domain.Repositories;
using Api.BoundedContexts.SystemConfiguration.Domain.ValueObjects;
using Api.Infrastructure;
using Api.SharedKernel.Application.Services;
using Api.SharedKernel.Infrastructure;
using Microsoft.EntityFrameworkCore;
using SystemConfigurationAggregate = Api.BoundedContexts.SystemConfiguration.Domain.Entities.SystemConfiguration;

namespace Api.BoundedContexts.SystemConfiguration.Infrastructure.Persistence;

public class ConfigurationRepository : RepositoryBase, IConfigurationRepository
{
    public ConfigurationRepository(MeepleAiDbContext dbContext, IDomainEventCollector eventCollector)
        : base(dbContext, eventCollector)
    {
    }

    public async Task<SystemConfigurationAggregate?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await DbContext.Set<Api.Infrastructure.Entities.SystemConfigurationEntity>()
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken).ConfigureAwait(false);

        return entity != null ? MapToDomain(entity) : null;
    }

    public async Task<IReadOnlyList<SystemConfigurationAggregate>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var entities = await DbContext.Set<Api.Infrastructure.Entities.SystemConfigurationEntity>()
            .AsNoTracking()
            .OrderBy(c => c.Key)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        return entities.Select(MapToDomain).ToList();
    }

    /// <summary>
    /// Retrieves a configuration by key with environment-specific fallback.
    /// </summary>
    /// <param name="key">The configuration key to search for.</param>
    /// <param name="environment">
    /// Optional environment filter. If null or empty, returns configs from ANY environment (no filtering).
    /// If specified, returns configs matching the exact environment OR environment="All",
    /// with exact environment matches prioritized over "All".
    /// </param>
    /// <param name="activeOnly">If true, only returns active configurations. If false, returns all configs.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The configuration if found, otherwise null.</returns>
    public async Task<SystemConfigurationAggregate?> GetByKeyAsync(string key, string? environment = null, bool activeOnly = true, CancellationToken cancellationToken = default)
    {
        var query = DbContext.Set<Api.Infrastructure.Entities.SystemConfigurationEntity>()
            .AsNoTracking()
            .Where(c => c.Key == key);

        // Filter by active status if requested
        if (activeOnly)
        {
            query = query.Where(c => c.IsActive);
        }

        // Filter and order by environment if specified
        if (!string.IsNullOrEmpty(environment))
        {
            // Filter: Match exact environment OR "All"
            query = query.Where(c => c.Environment == environment || c.Environment == "All");

            // Order: Prioritize environment-specific (0) over "All" (1)
            // Add tiebreakers for consistent ordering when multiple configs match
            query = query
                .OrderBy(c => c.Environment == environment ? 0 : 1)
                .ThenByDescending(c => c.UpdatedAt)
                .ThenByDescending(c => c.Version);
        }

        var entity = await query.FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);

        return entity != null ? MapToDomain(entity) : null;
    }

    public async Task<IReadOnlyList<SystemConfigurationAggregate>> GetByCategoryAsync(string category, CancellationToken cancellationToken = default)
    {
        var entities = await DbContext.Set<Api.Infrastructure.Entities.SystemConfigurationEntity>()
            .AsNoTracking()
            .Where(c => c.Category == category)
            .OrderBy(c => c.Key)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        return entities.Select(MapToDomain).ToList();
    }

    public async Task<IReadOnlyList<SystemConfigurationAggregate>> GetActiveConfigurationsAsync(CancellationToken cancellationToken = default)
    {
        var entities = await DbContext.Set<Api.Infrastructure.Entities.SystemConfigurationEntity>()
            .AsNoTracking()
            .Where(c => c.IsActive)
            .OrderBy(c => c.Key)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        return entities.Select(MapToDomain).ToList();
    }

    public async Task AddAsync(SystemConfigurationAggregate config, CancellationToken cancellationToken = default)
    {
        CollectDomainEvents(config);
        var entity = MapToPersistence(config);
        await DbContext.Set<Api.Infrastructure.Entities.SystemConfigurationEntity>().AddAsync(entity, cancellationToken).ConfigureAwait(false);
    }

    public Task UpdateAsync(SystemConfigurationAggregate config, CancellationToken cancellationToken = default)
    {
        CollectDomainEvents(config);
        var entity = MapToPersistence(config);
        DbContext.Set<Api.Infrastructure.Entities.SystemConfigurationEntity>().Update(entity);
        return Task.CompletedTask;
    }

    public Task DeleteAsync(SystemConfigurationAggregate config, CancellationToken cancellationToken = default)
    {
        var entity = MapToPersistence(config);
        DbContext.Set<Api.Infrastructure.Entities.SystemConfigurationEntity>().Remove(entity);
        return Task.CompletedTask;
    }

    public async Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await DbContext.Set<Api.Infrastructure.Entities.SystemConfigurationEntity>()
            .AnyAsync(c => c.Id == id, cancellationToken).ConfigureAwait(false);
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
