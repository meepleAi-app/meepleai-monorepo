using Api.BoundedContexts.SharedGameCatalog.Domain.Aggregates;

namespace Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;

/// <summary>
/// Repository interface for the <see cref="CertificationThresholdsConfig"/> aggregate (ADR-051 Sprint 1).
/// Provides singleton access to the operator-configurable certification threshold settings.
/// </summary>
public interface ICertificationThresholdsConfigRepository
{
    /// <summary>
    /// Returns the current certification thresholds configuration.
    /// Implementations must guarantee a non-null result (seed a default row on first access if needed).
    /// </summary>
    Task<CertificationThresholdsConfig> GetAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Persists an updated <see cref="CertificationThresholdsConfig"/>.
    /// Persistence occurs at <c>SaveChangesAsync</c>.
    /// </summary>
    Task UpdateAsync(CertificationThresholdsConfig config, CancellationToken cancellationToken = default);
}
