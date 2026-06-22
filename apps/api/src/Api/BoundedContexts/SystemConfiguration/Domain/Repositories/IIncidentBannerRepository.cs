using Api.BoundedContexts.SystemConfiguration.Domain.Entities;

namespace Api.BoundedContexts.SystemConfiguration.Domain.Repositories;

/// <summary>
/// Issue #1089: Repository for the singleton incident banner row.
/// </summary>
public interface IIncidentBannerRepository
{
    /// <summary>
    /// Returns the singleton row. Throws if missing (migration seeds the row).
    /// </summary>
    Task<IncidentBannerState> GetAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Persists changes to the singleton row.
    /// </summary>
    Task UpdateAsync(IncidentBannerState entity, CancellationToken cancellationToken = default);
}
