using Api.SharedKernel.Infrastructure.Persistence;
using SystemConfigurationEntity = Api.BoundedContexts.SystemConfiguration.Domain.Entities.SystemConfiguration;

namespace Api.BoundedContexts.SystemConfiguration.Domain.Repositories;

internal interface IConfigurationRepository : IRepository<SystemConfigurationEntity, Guid>
{
    Task<SystemConfigurationEntity?> GetByKeyAsync(string key, string? environment = null, bool activeOnly = true, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SystemConfigurationEntity>> GetByCategoryAsync(string category, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SystemConfigurationEntity>> GetActiveConfigurationsAsync(CancellationToken cancellationToken = default);
}
