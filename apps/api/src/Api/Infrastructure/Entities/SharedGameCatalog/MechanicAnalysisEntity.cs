namespace Api.Infrastructure.Entities.SharedGameCatalog;

/// <summary>
/// Persistence entity for <see cref="Api.BoundedContexts.SharedGameCatalog.Domain.Aggregates.MechanicAnalysis"/>
/// (ADR-051 / M1.1). Plain POCO — all invariants are enforced by the domain aggregate; this type
/// exists only to be shaped by EF Core.
/// </summary>
public class MechanicAnalysisEntity
{
    public Guid Id { get; set; }

    // === Core identity / lineage ===
    public Guid SharedGameId { get; set; }
    public Guid PdfDocumentId { get; set; }
    public string PromptVersion { get; set; } = string.Empty;

    // === Lifecycle ===
    /// <summary>0=Draft, 1=InReview, 2=Published, 3=Rejected.</summary>
    public int Status { get; set; }
    public Guid CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; }
    public Guid? ReviewedBy { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public string? RejectionReason { get; set; }

    // === LLM execution snapshot ===
    public int TotalTokensUsed { get; set; }
    public decimal EstimatedCostUsd { get; set; }
    public string ModelUsed { get; set; } = string.Empty;
    public string Provider { get; set; } = string.Empty;

    // === T8 cost governance ===
    public decimal CostCapUsd { get; set; }
    public DateTime? CostCapOverrideAt { get; set; }
    public Guid? CostCapOverrideBy { get; set; }
    public string? CostCapOverrideReason { get; set; }

    // === T5 takedown kill-switch ===
    public bool IsSuppressed { get; set; }
    public DateTime? SuppressedAt { get; set; }
    public Guid? SuppressedBy { get; set; }
    public string? SuppressionReason { get; set; }
    public DateTime? SuppressionRequestedAt { get; set; }
    /// <summary>0=Publisher, 1=Legal, 2=Community, 3=Internal.</summary>
    public int? SuppressionRequestSource { get; set; }

    // === AI comprehension certification (ADR-051 M2) ===
    /// <summary>0=NotEvaluated, 1=Certified, 2=NotCertified.
    /// Mirrors <c>Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects.CertificationStatus</c>.</summary>
    public int CertificationStatus { get; set; }
    public DateTimeOffset? CertifiedAt { get; set; }
    public Guid? CertifiedByUserId { get; set; }
    public string? CertificationOverrideReason { get; set; }
    public Guid? LastMetricsId { get; set; }

    // === Optimistic concurrency ===
    /// <summary>
    /// PostgreSQL system column <c>xmin</c> mapped as <see cref="uint"/> (xid).
    /// Marked as concurrency token in EF configuration; server-generated on add/update.
    /// </summary>
    public uint Xmin { get; set; }

    // === Navigation ===
    public SharedGameEntity SharedGame { get; set; } = default!;
    public ICollection<MechanicClaimEntity> Claims { get; set; } = new List<MechanicClaimEntity>();
    public ICollection<MechanicAnalysisSectionRunEntity> SectionRuns { get; set; } = new List<MechanicAnalysisSectionRunEntity>();
}
