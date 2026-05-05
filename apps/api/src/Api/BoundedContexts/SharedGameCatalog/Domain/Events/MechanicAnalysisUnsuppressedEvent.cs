using Api.SharedKernel.Domain.Events;

namespace Api.BoundedContexts.SharedGameCatalog.Domain.Events;

/// <summary>
/// Raised when a takedown on a <see cref="Aggregates.MechanicAnalysis"/> is lifted.
/// Consumed by the SaveChanges interceptor to persist a
/// <see cref="Entities.MechanicSuppressionAudit"/> row (toggle true→false).
/// </summary>
internal sealed class MechanicAnalysisUnsuppressedEvent : DomainEventBase
{
    public Guid AnalysisId { get; }

    public Guid ActorId { get; }

    public string Reason { get; }

    public MechanicAnalysisUnsuppressedEvent(Guid analysisId, Guid actorId, string reason)
    {
        AnalysisId = analysisId;
        ActorId = actorId;
        Reason = reason;
    }
}
