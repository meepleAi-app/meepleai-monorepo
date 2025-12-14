using Api.BoundedContexts.Administration.Domain.Aggregates.AlertConfigurations;
using Api.BoundedContexts.Administration.Domain.Repositories;
using Api.Infrastructure;
using Api.Infrastructure.Entities.Administration;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.Administration.Infrastructure.Repositories;

public class AlertConfigurationRepository : IAlertConfigurationRepository
{
    private readonly MeepleAiDbContext _context;
    public AlertConfigurationRepository(MeepleAiDbContext context) => _context = context;

    public async Task<AlertConfiguration?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await _context.AlertConfigurations.FindAsync(new object[] { id }, ct).ConfigureAwait(false);
        return entity == null ? null : MapToDomain(entity);
    }

    public async Task<AlertConfiguration?> GetByKeyAsync(string configKey, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(configKey);
        var entity = await _context.AlertConfigurations.FirstOrDefaultAsync(c => c.ConfigKey == configKey, ct).ConfigureAwait(false);
        return entity == null ? null : MapToDomain(entity);
    }

    public async Task<List<AlertConfiguration>> GetByCategoryAsync(ConfigCategory category, CancellationToken ct = default) =>
        (await _context.AlertConfigurations.Where(c => c.Category == category.ToDisplayString()).ToListAsync(ct).ConfigureAwait(false)).Select(MapToDomain).ToList();

    public async Task<List<AlertConfiguration>> GetAllAsync(CancellationToken ct = default) =>
        (await _context.AlertConfigurations.ToListAsync(ct).ConfigureAwait(false)).Select(MapToDomain).ToList();

    public async Task AddAsync(AlertConfiguration config, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(config);
        await _context.AlertConfigurations.AddAsync(MapToEntity(config), ct).ConfigureAwait(false);
        await _context.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    public async Task UpdateAsync(AlertConfiguration config, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(config);
        var entity = await _context.AlertConfigurations.FindAsync(new object[] { config.Id }, ct).ConfigureAwait(false);
        if (entity == null) throw new InvalidOperationException($"AlertConfiguration {config.Id} not found");
        entity.ConfigValue = config.ConfigValue;
        entity.IsEncrypted = config.IsEncrypted;
        entity.Description = config.Description;
        entity.UpdatedAt = config.UpdatedAt;
        entity.UpdatedBy = config.UpdatedBy;
        await _context.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await _context.AlertConfigurations.FindAsync(new object[] { id }, ct).ConfigureAwait(false);
        if (entity != null) { _context.AlertConfigurations.Remove(entity); await _context.SaveChangesAsync(ct).ConfigureAwait(false); }
    }

    private static AlertConfiguration MapToDomain(AlertConfigurationEntity e) => AlertConfiguration.Create(e.ConfigKey, e.ConfigValue, ConfigCategoryExtensions.FromString(e.Category), e.UpdatedBy, e.Description);

    private static AlertConfigurationEntity MapToEntity(AlertConfiguration c) => new() { Id = c.Id, ConfigKey = c.ConfigKey, ConfigValue = c.ConfigValue, Category = c.Category.ToDisplayString(), IsEncrypted = c.IsEncrypted, Description = c.Description, UpdatedAt = c.UpdatedAt, UpdatedBy = c.UpdatedBy };
}
