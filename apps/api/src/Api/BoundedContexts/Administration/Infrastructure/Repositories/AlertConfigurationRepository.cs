using Api.BoundedContexts.Administration.Domain.Aggregates.AlertConfigurations;
using Api.BoundedContexts.Administration.Domain.Repositories;
using Api.Infrastructure;
using Api.SharedKernel.Application.Services;
using Api.SharedKernel.Infrastructure;
using Api.Infrastructure.Entities.Administration;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.Administration.Infrastructure.Repositories;

internal class AlertConfigurationRepository : RepositoryBase, IAlertConfigurationRepository
{
    public AlertConfigurationRepository(MeepleAiDbContext dbContext, IDomainEventCollector eventCollector)
        : base(dbContext, eventCollector)
    {
    }

    public async Task<AlertConfiguration?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await DbContext.AlertConfigurations.FindAsync(new object[] { id }, cancellationToken).ConfigureAwait(false);
        return entity == null ? null : MapToDomain(entity);
    }

    public async Task<AlertConfiguration?> GetByKeyAsync(string configKey, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(configKey);
        var entity = await DbContext.AlertConfigurations.FirstOrDefaultAsync(c => c.ConfigKey == configKey, cancellationToken).ConfigureAwait(false);
        return entity == null ? null : MapToDomain(entity);
    }

    public async Task<List<AlertConfiguration>> GetByCategoryAsync(ConfigCategory category, CancellationToken cancellationToken = default) =>
        (await DbContext.AlertConfigurations.Where(c => c.Category == category.ToDisplayString()).ToListAsync(cancellationToken).ConfigureAwait(false)).Select(MapToDomain).ToList();

    public async Task<List<AlertConfiguration>> GetAllAsync(CancellationToken cancellationToken = default) =>
        (await DbContext.AlertConfigurations.ToListAsync(cancellationToken).ConfigureAwait(false)).Select(MapToDomain).ToList();

    public async Task AddAsync(AlertConfiguration config, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(config);
        await DbContext.AlertConfigurations.AddAsync(MapToEntity(config), cancellationToken).ConfigureAwait(false);
        await DbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task UpdateAsync(AlertConfiguration config, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(config);
        var entity = await DbContext.AlertConfigurations.FindAsync(new object[] { config.Id }, cancellationToken).ConfigureAwait(false);
        if (entity == null) throw new InvalidOperationException($"AlertConfiguration {config.Id} not found");
        entity.ConfigValue = config.ConfigValue;
        entity.IsEncrypted = config.IsEncrypted;
        entity.Description = config.Description;
        entity.UpdatedAt = config.UpdatedAt;
        entity.UpdatedBy = config.UpdatedBy;
        await DbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await DbContext.AlertConfigurations.FindAsync(new object[] { id }, cancellationToken).ConfigureAwait(false);
        if (entity != null) { DbContext.AlertConfigurations.Remove(entity); await DbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false); }
    }

    private static AlertConfiguration MapToDomain(AlertConfigurationEntity e) => AlertConfiguration.Create(e.ConfigKey, e.ConfigValue, ConfigCategoryExtensions.FromString(e.Category), e.UpdatedBy, e.Description);

    private static AlertConfigurationEntity MapToEntity(AlertConfiguration c) => new() { Id = c.Id, ConfigKey = c.ConfigKey, ConfigValue = c.ConfigValue, Category = c.Category.ToDisplayString(), IsEncrypted = c.IsEncrypted, Description = c.Description, UpdatedAt = c.UpdatedAt, UpdatedBy = c.UpdatedBy };
}

