namespace Api.Infrastructure.Entities.SharedGameCatalog;

/// <summary>
/// Audit row for every suppression / unsuppression action on a <see cref="MechanicAnalysisEntity"/>
/// (ADR-051 T5). Written atomically by the repository.
/// </summary>
public class MechanicSuppressionAuditEntity
{
    public Guid Id { get; set; }
    public Guid AnalysisId { get; set; }

    /// <summary>true = suppression applied, false = suppression lifted.</summary>
    public bool IsSuppressed { get; set; }

    public Guid ActorId { get; set; }

    public string Reason { get; set; } = string.Empty;

    /// <summary>Source code: 0=Publisher, 1=Legal, 2=Community, 3=Internal. Null when unsuppressing.</summary>
    public int? RequestSource { get; set; }

    public DateTime? RequestedAt { get; set; }

    public DateTime OccurredAt { get; set; }

    // === Navigation ===
    public MechanicAnalysisEntity Analysis { get; set; } = default!;
}
