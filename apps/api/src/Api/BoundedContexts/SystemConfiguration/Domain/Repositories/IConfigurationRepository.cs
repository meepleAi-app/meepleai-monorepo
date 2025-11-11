using Api.SharedKernel.Infrastructure.Persistence;
using SystemConfigurationEntity = Api.BoundedContexts.SystemConfiguration.Domain.Entities.SystemConfiguration;

namespace Api.BoundedContexts.SystemConfiguration.Domain.Repositories;

public interface IConfigurationRepository : IRepository<SystemConfigurationEntity, Guid>
{
    Task<SystemConfigurationEntity?> GetByKeyAsync(string key, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SystemConfigurationEntity>> GetByCategoryAsync(string category, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SystemConfigurationEntity>> GetActiveConfigurationsAsync(CancellationToken cancellationToken = default);
}
