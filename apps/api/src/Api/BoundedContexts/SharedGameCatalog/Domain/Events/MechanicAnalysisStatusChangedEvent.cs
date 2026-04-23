using Api.BoundedContexts.SharedGameCatalog.Domain.Enums;
using Api.SharedKernel.Domain.Events;

namespace Api.BoundedContexts.SharedGameCatalog.Domain.Events;

/// <summary>
/// Raised when a <see cref="Aggregates.MechanicAnalysis"/> lifecycle status transitions
/// (Draft → InReview, InReview → Published, InReview → Rejected, Rejected → InReview).
/// </summary>
/// <remarks>
/// Consumer: <c>MediatorSaveChangesInterceptor</c> writes a row into <c>mechanic_status_audit</c>
/// in the same transaction as the aggregate's state change (atomicity invariant from §2.3 of the plan).
/// </remarks>
internal sealed class MechanicAnalysisStatusChangedEvent : DomainEventBase
{
    public Guid AnalysisId { get; }

    public MechanicAnalysisStatus FromStatus { get; }

    public MechanicAnalysisStatus ToStatus { get; }

    public Guid ActorId { get; }

    public string? Note { get; }

    public MechanicAnalysisStatusChangedEvent(
        Guid analysisId,
        MechanicAnalysisStatus fromStatus,
        MechanicAnalysisStatus toStatus,
        Guid actorId,
        string? note)
    {
        AnalysisId = analysisId;
        FromStatus = fromStatus;
        ToStatus = toStatus;
        ActorId = actorId;
        Note = note;
    }
}
