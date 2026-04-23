namespace Api.Infrastructure.Entities.SharedGameCatalog;

/// <summary>
/// Audit row for every <see cref="MechanicAnalysisEntity.Status"/> transition (ADR-051 T6).
/// Written by the repository from the aggregate's collected <c>MechanicAnalysisStatusChangedEvent</c>s
/// in the same <c>SaveChangesAsync</c> transaction as the analysis update, ensuring atomicity.
/// </summary>
public class MechanicStatusAuditEntity
{
    public Guid Id { get; set; }
    public Guid AnalysisId { get; set; }

    /// <summary>Previous lifecycle status code.</summary>
    public int FromStatus { get; set; }

    /// <summary>New lifecycle status code.</summary>
    public int ToStatus { get; set; }

    public Guid ActorId { get; set; }

    /// <summary>Optional admin note (e.g. rejection reason).</summary>
    public string? Note { get; set; }

    public DateTime OccurredAt { get; set; }

    // === Navigation ===
    public MechanicAnalysisEntity Analysis { get; set; } = default!;
}
