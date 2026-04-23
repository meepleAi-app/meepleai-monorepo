using Api.SharedKernel.Domain.Events;

namespace Api.BoundedContexts.SharedGameCatalog.Domain.Events;

/// <summary>
/// Raised when an admin explicitly raises the cost cap for a <see cref="Aggregates.MechanicAnalysis"/>
/// beyond the default (ADR-051 T8). Consumed by the SaveChanges interceptor for audit persistence.
/// </summary>
internal sealed class MechanicAnalysisCostCapOverriddenEvent : DomainEventBase
{
    public Guid AnalysisId { get; }

    public Guid ActorId { get; }

    public decimal PreviousCapUsd { get; }

    public decimal NewCapUsd { get; }

    public string Reason { get; }

    public MechanicAnalysisCostCapOverriddenEvent(
        Guid analysisId,
        Guid actorId,
        decimal previousCapUsd,
        decimal newCapUsd,
        string reason)
    {
        AnalysisId = analysisId;
        ActorId = actorId;
        PreviousCapUsd = previousCapUsd;
        NewCapUsd = newCapUsd;
        Reason = reason;
    }
}
