using Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;

namespace Api.BoundedContexts.SharedGameCatalog.Domain.Aggregates;

public sealed class CertificationThresholdsConfig
{
    public int Id { get; private set; } = 1;
    public CertificationThresholds Thresholds { get; private set; } = CertificationThresholds.Default();
    public DateTimeOffset UpdatedAt { get; private set; }
    public Guid? UpdatedByUserId { get; private set; }

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
}
