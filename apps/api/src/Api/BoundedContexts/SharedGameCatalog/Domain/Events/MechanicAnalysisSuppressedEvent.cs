using Api.BoundedContexts.SharedGameCatalog.Domain.Enums;
using Api.SharedKernel.Domain.Events;

namespace Api.BoundedContexts.SharedGameCatalog.Domain.Events;

/// <summary>
/// Raised when a <see cref="Aggregates.MechanicAnalysis"/> is suppressed in response to a takedown request
/// (ADR-051 T5 kill-switch). Consumed by the SaveChanges interceptor to persist a
/// <see cref="Entities.MechanicSuppressionAudit"/> row in the same transaction.
/// </summary>
internal sealed class MechanicAnalysisSuppressedEvent : DomainEventBase
{
    public Guid AnalysisId { get; }

    public Guid ActorId { get; }

    public string Reason { get; }

    public SuppressionRequestSource RequestSource { get; }

    public DateTime? RequestedAt { get; }

    public MechanicAnalysisSuppressedEvent(
        Guid analysisId,
        Guid actorId,
        string reason,
        SuppressionRequestSource requestSource,
        DateTime? requestedAt)
    {
        AnalysisId = analysisId;
        ActorId = actorId;
        Reason = reason;
        RequestSource = requestSource;
        RequestedAt = requestedAt;
    }
}
