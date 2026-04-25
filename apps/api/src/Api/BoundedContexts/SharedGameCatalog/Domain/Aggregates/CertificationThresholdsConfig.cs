using Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;

namespace Api.BoundedContexts.SharedGameCatalog.Domain.Aggregates;

public sealed class CertificationThresholdsConfig
{
    public int Id { get; private set; } = 1;
    public CertificationThresholds Thresholds { get; private set; } = CertificationThresholds.Default();
    public DateTimeOffset UpdatedAt { get; private set; }
    public Guid? UpdatedByUserId { get; private set; }

    /// <summary>
    /// PostgreSQL <c>xmin</c> system-column snapshot used as the optimistic concurrency token.
    /// Captured on load from the repository and re-attached on update so EF raises
    /// <c>DbUpdateConcurrencyException</c> when a concurrent write has advanced the row's xmin.
    /// </summary>
    public uint XminVersion { get; private set; }

    private CertificationThresholdsConfig() { }

    public static CertificationThresholdsConfig Seed() => new()
    {
        Id = 1,
        Thresholds = CertificationThresholds.Default(),
        UpdatedAt = DateTimeOffset.UtcNow,
        UpdatedByUserId = null,
    };

    public void Update(CertificationThresholds thresholds, Guid updatedByUserId)
    {
        ArgumentNullException.ThrowIfNull(thresholds);
        Thresholds = thresholds;
        UpdatedAt = DateTimeOffset.UtcNow;
        UpdatedByUserId = updatedByUserId;
    }

    /// <summary>
    /// Rehydrates the singleton configuration from persistence. Used exclusively by the repository's mapping layer.
    /// </summary>
    public static CertificationThresholdsConfig Reconstitute(
        CertificationThresholds thresholds,
        DateTimeOffset updatedAt,
        Guid? updatedByUserId,
        uint xminVersion = 0)
    {
        ArgumentNullException.ThrowIfNull(thresholds);
        return new CertificationThresholdsConfig
        {
            Id = 1,
            Thresholds = thresholds,
            UpdatedAt = updatedAt,
            UpdatedByUserId = updatedByUserId,
            XminVersion = xminVersion,
        };
    }
}
