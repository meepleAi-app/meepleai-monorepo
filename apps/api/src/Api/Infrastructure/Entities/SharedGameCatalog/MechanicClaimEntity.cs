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

    // === Navigation ===
    public MechanicAnalysisEntity Analysis { get; set; } = default!;
    public ICollection<MechanicCitationEntity> Citations { get; set; } = new List<MechanicCitationEntity>();
}
