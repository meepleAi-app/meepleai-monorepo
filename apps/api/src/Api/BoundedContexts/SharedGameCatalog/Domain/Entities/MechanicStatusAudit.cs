using Api.BoundedContexts.SharedGameCatalog.Domain.Enums;
using Api.SharedKernel.Domain.Entities;

namespace Api.BoundedContexts.SharedGameCatalog.Domain.Entities;

/// <summary>
/// Immutable audit row recording a lifecycle transition of a <see cref="Aggregates.MechanicAnalysis"/>
/// (Draft → InReview → Published / Rejected).
/// </summary>
/// <remarks>
/// Persisted by the SaveChanges interceptor in response to
/// <see cref="Events.MechanicAnalysisStatusChangedEvent"/> events emitted by the aggregate.
/// Append-only: there is no update surface.
/// Suppression events are recorded separately in <see cref="MechanicSuppressionAudit"/> — this
/// split keeps the schema strict (no nullable discriminator columns) and indices partial.
/// </remarks>
public sealed class MechanicStatusAudit : Entity<Guid>
{
    /// <summary>FK to the analysis that transitioned.</summary>
    public Guid AnalysisId { get; private set; }

    /// <summary>Status before the transition.</summary>
    public MechanicAnalysisStatus FromStatus { get; private set; }

    /// <summary>Status after the transition. Must differ from <see cref="FromStatus"/>.</summary>
    public MechanicAnalysisStatus ToStatus { get; private set; }

    /// <summary>User (admin / system bot) who performed the transition.</summary>
    public Guid ActorId { get; private set; }

    /// <summary>Optional explanation (e.g. rejection reason).</summary>
    public string? Note { get; private set; }

    /// <summary>UTC timestamp the transition actually took effect.</summary>
    public DateTime OccurredAt { get; private set; }

    /// <summary>EF Core constructor.</summary>
    private MechanicStatusAudit() : base()
    {
    }

    private MechanicStatusAudit(
        Guid id,
        Guid analysisId,
        MechanicAnalysisStatus fromStatus,
        MechanicAnalysisStatus toStatus,
        Guid actorId,
        string? note,
        DateTime occurredAt)
        : base(id)
    {
        AnalysisId = analysisId;
        FromStatus = fromStatus;
        ToStatus = toStatus;
        ActorId = actorId;
        Note = note;
        OccurredAt = occurredAt;
    }

    /// <summary>
    /// Creates a new status-transition audit row. Called from the SaveChanges interceptor
    /// after it consumes a <see cref="Events.MechanicAnalysisStatusChangedEvent"/>.
    /// </summary>
    public static MechanicStatusAudit Create(
        Guid analysisId,
        MechanicAnalysisStatus fromStatus,
        MechanicAnalysisStatus toStatus,
        Guid actorId,
        string? note,
        DateTime occurredAt)
    {
        if (analysisId == Guid.Empty)
        {
            throw new ArgumentException("AnalysisId cannot be empty.", nameof(analysisId));
        }

        if (actorId == Guid.Empty)
        {
            throw new ArgumentException("ActorId cannot be empty.", nameof(actorId));
        }

        if (fromStatus == toStatus)
        {
            throw new ArgumentException(
                $"Status transition cannot be a self-loop ({fromStatus} → {toStatus}).",
                nameof(toStatus));
        }

        return new MechanicStatusAudit(
            id: Guid.NewGuid(),
            analysisId: analysisId,
            fromStatus: fromStatus,
            toStatus: toStatus,
            actorId: actorId,
            note: string.IsNullOrWhiteSpace(note) ? null : note.Trim(),
            occurredAt: occurredAt);
    }
}
