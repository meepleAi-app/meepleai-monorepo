using Api.BoundedContexts.SystemConfiguration.Domain.Entities;
using Api.BoundedContexts.SystemConfiguration.Domain.Repositories;
using Api.Infrastructure;
using Api.SharedKernel.Application.Services;
using Api.SharedKernel.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.SystemConfiguration.Infrastructure.Persistence;

/// <summary>
/// Feature flag repository - stores flags as SystemConfiguration entries with "Features:" prefix.
/// </summary>
internal class FeatureFlagRepository : RepositoryBase, IFeatureFlagRepository
{
    private const string FlagPrefix = "Features:";

    public FeatureFlagRepository(MeepleAiDbContext dbContext, IDomainEventCollector eventCollector)
        : base(dbContext, eventCollector)
    {
    }

    public async Task<FeatureFlag?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await DbContext.Set<Api.Infrastructure.Entities.SystemConfigurationEntity>()
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == id && c.Key.StartsWith(FlagPrefix), cancellationToken).ConfigureAwait(false);

        return entity != null ? MapToDomain(entity) : null;
    }

    public async Task<IReadOnlyList<FeatureFlag>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var entities = await DbContext.Set<Api.Infrastructure.Entities.SystemConfigurationEntity>()
            .AsNoTracking()
            .Where(c => c.Key.StartsWith(FlagPrefix))
            .OrderBy(c => c.Key)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        return entities.Select(MapToDomain).ToList();
    }

    public async Task<FeatureFlag?> GetByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        var key = $"{FlagPrefix}{name}";
        var entity = await DbContext.Set<Api.Infrastructure.Entities.SystemConfigurationEntity>()
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Key == key, cancellationToken).ConfigureAwait(false);

        return entity != null ? MapToDomain(entity) : null;
    }

    public async Task<IReadOnlyList<FeatureFlag>> GetEnabledFlagsAsync(CancellationToken cancellationToken = default)
    {
        var entities = await DbContext.Set<Api.Infrastructure.Entities.SystemConfigurationEntity>()
            .AsNoTracking()
            .Where(c => c.Key.StartsWith(FlagPrefix) && c.IsActive && c.Value == "true")
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        return entities.Select(MapToDomain).ToList();
    }

    public async Task AddAsync(FeatureFlag flag, CancellationToken cancellationToken = default)
    {
        CollectDomainEvents(flag);
        var entity = MapToPersistence(flag);
        await DbContext.Set<Api.Infrastructure.Entities.SystemConfigurationEntity>().AddAsync(entity, cancellationToken).ConfigureAwait(false);
    }

    public Task UpdateAsync(FeatureFlag flag, CancellationToken cancellationToken = default)
    {
        CollectDomainEvents(flag);
        var entity = MapToPersistence(flag);
        DbContext.Set<Api.Infrastructure.Entities.SystemConfigurationEntity>().Update(entity);
        return Task.CompletedTask;
    }

    public Task DeleteAsync(FeatureFlag flag, CancellationToken cancellationToken = default)
    {
        var entity = MapToPersistence(flag);
        DbContext.Set<Api.Infrastructure.Entities.SystemConfigurationEntity>().Remove(entity);
        return Task.CompletedTask;
    }

    public async Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await DbContext.Set<Api.Infrastructure.Entities.SystemConfigurationEntity>()
            .AnyAsync(c => c.Id == id && c.Key.StartsWith(FlagPrefix), cancellationToken).ConfigureAwait(false);
    }

    private static FeatureFlag MapToDomain(Api.Infrastructure.Entities.SystemConfigurationEntity entity)
    {
        var name = entity.Key.Substring(FlagPrefix.Length);
        var isEnabled = string.Equals(entity.Value, "true", StringComparison.Ordinal) && entity.IsActive;

        var flag = new FeatureFlag(
            id: entity.Id,
            name: name,
            isEnabled: isEnabled,
            description: entity.Description
        );

        // Override timestamps
        var createdAtProp = typeof(FeatureFlag).GetProperty("CreatedAt");
        createdAtProp?.SetValue(flag, entity.CreatedAt);

        var updatedAtProp = typeof(FeatureFlag).GetProperty("UpdatedAt");
        updatedAtProp?.SetValue(flag, entity.UpdatedAt);

        return flag;
    }

    private static Api.Infrastructure.Entities.SystemConfigurationEntity MapToPersistence(FeatureFlag domain)
    {
        return new Api.Infrastructure.Entities.SystemConfigurationEntity
        {
            Id = domain.Id,
            Key = $"{FlagPrefix}{domain.Name}",
            Value = domain.IsEnabled ? "true" : "false",
            ValueType = "bool",
            Description = domain.Description,
            Category = "Features",
            IsActive = true,
            RequiresRestart = false,
            Environment = "All",
            Version = 1,
            CreatedAt = domain.CreatedAt,
            UpdatedAt = domain.UpdatedAt,
            CreatedByUserId = Guid.Empty // Feature flags don't track creator
        };
    }
}
