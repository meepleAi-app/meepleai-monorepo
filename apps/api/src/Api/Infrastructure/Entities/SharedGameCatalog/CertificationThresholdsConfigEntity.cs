namespace Api.Infrastructure.Entities.SharedGameCatalog;

/// <summary>
/// Persistence entity for <see cref="Api.BoundedContexts.SharedGameCatalog.Domain.Aggregates.CertificationThresholdsConfig"/>.
/// Plain POCO — all invariants are enforced by the domain aggregate; this type exists only to be shaped by EF Core.
/// Singleton row (Id = 1). The <c>CertificationThresholds</c> value object is flattened into four primitive columns.
/// </summary>
public class CertificationThresholdsConfigEntity
{
    /// <summary>Always 1 — singleton configuration row.</summary>
    public int Id { get; set; } = 1;

    // === CertificationThresholds (flattened) ===
    public decimal MinCoveragePct { get; set; }
    public int MaxPageTolerance { get; set; }
    public decimal MinBggMatchPct { get; set; }
    public decimal MinOverallScore { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
    public Guid? UpdatedByUserId { get; set; }

    // === Optimistic concurrency ===
    /// <summary>
    /// PostgreSQL system column <c>xmin</c> mapped as <see cref="uint"/> (xid).
    /// Marked as concurrency token in EF configuration; server-generated on add/update.
    /// </summary>
    public uint Xmin { get; set; }
}
