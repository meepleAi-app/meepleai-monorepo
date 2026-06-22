using Api.BoundedContexts.SharedGameCatalog.Domain.Enums;
using Api.SharedKernel.Domain.Events;

namespace Api.BoundedContexts.SharedGameCatalog.Domain.Events;

/// <summary>
/// Raised when the cost cap for a <see cref="Aggregates.MechanicAnalysis"/> is overridden:
/// either by an admin explicit override (ADR-051 T8 — admin lifts the cap beyond the default)
/// or by the pipeline detecting a mid-stream overrun (#2494 AC-5 — cumulative cost exceeds
/// the cap AFTER at least one section completed successfully).
/// Consumed by the SaveChanges interceptor for audit persistence.
/// </summary>
internal sealed class MechanicAnalysisCostCapOverriddenEvent : DomainEventBase
{
    public Guid AnalysisId { get; }

    public Guid ActorId { get; }

    public decimal PreviousCapUsd { get; }

    public decimal NewCapUsd { get; }

    public string Reason { get; }

    /// <summary>
    /// #2494 AC-5: semantic categorization of the cost-cap event.
    /// Defaults to <see cref="CostCapOverrunCause.AdminOverride"/> for back-compat
    /// with admin override callers that don't pass an explicit cause.
    /// </summary>
    public CostCapOverrunCause OverrunCause { get; }

    public MechanicAnalysisCostCapOverriddenEvent(
        Guid analysisId,
        Guid actorId,
        decimal previousCapUsd,
        decimal newCapUsd,
        string reason,
        CostCapOverrunCause overrunCause = CostCapOverrunCause.AdminOverride)
    {
        AnalysisId = analysisId;
        ActorId = actorId;
        PreviousCapUsd = previousCapUsd;
        NewCapUsd = newCapUsd;
        Reason = reason;
        OverrunCause = overrunCause;
    }
}
