namespace Api.Infrastructure.Entities.SharedGameCatalog;

/// <summary>
/// Persistence entity for <see cref="Api.BoundedContexts.SharedGameCatalog.Domain.Aggregates.MechanicCard"/>
/// (ADR-051 / #527). Plain POCO — all invariants live in the domain aggregate.
/// </summary>
public class MechanicCardEntity
{
    public Guid Id { get; set; }

    // === Identity / lineage ===
    public Guid SharedGameId { get; set; }
    public Guid OriginAnalysisId { get; set; }

    /// <summary>Lowercase string: 'ai_reviewed' | 'manual' | 'imported_external' (DB CHECK).</summary>
    public string Origin { get; set; } = "ai_reviewed";

    // === Content ===
    public string Title { get; set; } = string.Empty;

    /// <summary>Immutable JSONB snapshot of the approved claims (AD-1).</summary>
    public string Content { get; set; } = "{}";

    public int Version { get; set; }

    // === Takedown (T5) ===
    public bool IsSuppressed { get; set; }
    public string? SuppressedReason { get; set; }
    public DateTime? SuppressedAt { get; set; }
    public Guid? SuppressedBy { get; set; }

    // === Player feedback (#531) ===
    public int ErrorReportsCount { get; set; }
    public decimal? FeedbackScore { get; set; }

    // === Publication audit ===
    public DateTime PublishedAt { get; set; }
    public Guid PublishedBy { get; set; }

    // === Housekeeping ===
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    /// <summary>PostgreSQL system column <c>xmin</c> mapped as <see cref="uint"/> (xid) — concurrency token (ADR-060).</summary>
    public uint Xmin { get; set; }

    // === Navigation ===
    public SharedGameEntity SharedGame { get; set; } = default!;
    public MechanicAnalysisEntity OriginAnalysis { get; set; } = default!;
    public ICollection<MechanicCardAuditLogEntity> AuditLog { get; set; } = new List<MechanicCardAuditLogEntity>();
}
