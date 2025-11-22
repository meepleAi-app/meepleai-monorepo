using Api.BoundedContexts.SystemConfiguration.Domain.Entities;
using Api.SharedKernel.Infrastructure.Persistence;

namespace Api.BoundedContexts.SystemConfiguration.Domain.Repositories;

public interface IFeatureFlagRepository : IRepository<FeatureFlag, Guid>
{
    Task<FeatureFlag?> GetByNameAsync(string name, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<FeatureFlag>> GetEnabledFlagsAsync(CancellationToken cancellationToken = default);
}
