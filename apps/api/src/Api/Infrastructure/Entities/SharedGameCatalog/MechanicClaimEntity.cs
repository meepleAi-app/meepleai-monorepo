using Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;

namespace Api.Infrastructure.Entities.SharedGameCatalog;

/// <summary>
/// Persistence entity for <see cref="Api.BoundedContexts.SharedGameCatalog.Domain.Entities.MechanicClaim"/>.
/// </summary>
public class MechanicClaimEntity
{
    public Guid Id { get; set; }
    public Guid AnalysisId { get; set; }

    /// <summary>0=Summary, 1=Mechanics, 2=Victory, 3=Resources, 4=Phases, 5=Questions.</summary>
    public int Section { get; set; }

    public string Text { get; set; } = string.Empty;
    public int DisplayOrder { get; set; }

    /// <summary>0=Pending, 1=Approved, 2=Rejected.</summary>
    public int Status { get; set; }

    public Guid? ReviewedBy { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public string? RejectionNote { get; set; }

    /// <summary>Optional free-form note captured on APPROVE (#526 AC-6). Distinct from RejectionNote.</summary>
    public string? ReviewNote { get; set; }

    /// <summary>
    /// Real per-claim guardrail outcomes captured at pipeline time (#2782 FU-1). Null for pre-FU-1
    /// claims (legacy all-pass derivation applies). Stored as jsonb via a value converter. No value
    /// comparer is needed because MechanicAnalysisRepository.Update() rebuilds a detached entity and
    /// force-writes all columns (the snapshot-diff a comparer feeds is never consulted for that path).
    /// </summary>
    public List<MechanicClaimValidation>? Validations { get; set; }

    // === Navigation ===
    public MechanicAnalysisEntity Analysis { get; set; } = default!;
    public ICollection<MechanicCitationEntity> Citations { get; set; } = new List<MechanicCitationEntity>();
}
