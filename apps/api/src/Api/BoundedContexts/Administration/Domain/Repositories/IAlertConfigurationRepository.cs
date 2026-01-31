using Api.BoundedContexts.Administration.Domain.Aggregates.AlertConfigurations;

namespace Api.BoundedContexts.Administration.Domain.Repositories;

/// <summary>
/// Repository for AlertConfiguration aggregate (Issue #921)
/// </summary>
internal interface IAlertConfigurationRepository
{
    Task<AlertConfiguration?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<AlertConfiguration?> GetByKeyAsync(string configKey, CancellationToken cancellationToken = default);
    Task<List<AlertConfiguration>> GetByCategoryAsync(ConfigCategory category, CancellationToken cancellationToken = default);
    Task<List<AlertConfiguration>> GetAllAsync(CancellationToken cancellationToken = default);
    Task AddAsync(AlertConfiguration config, CancellationToken cancellationToken = default);
    Task UpdateAsync(AlertConfiguration config, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}

