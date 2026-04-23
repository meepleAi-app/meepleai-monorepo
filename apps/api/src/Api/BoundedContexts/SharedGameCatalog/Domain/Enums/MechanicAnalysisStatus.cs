namespace Api.BoundedContexts.SharedGameCatalog.Domain.Enums;

/// <summary>
/// Lifecycle status of a MechanicAnalysis.
/// Transitions (see <see cref="Aggregates.MechanicAnalysis"/> state machine):
///   Draft (0) → InReview (1): SubmitForReview
///   InReview (1) → Published (2): Approve (all claims approved)
///   InReview (1) → Rejected (3): Reject
///   Rejected (3) → InReview (1): SubmitForReview (after edits)
/// Note: Suppressed is NOT a Status value — suppression is orthogonal via <see cref="Aggregates.MechanicAnalysis.IsSuppressed"/>.
/// A Published analysis under takedown stays Published + IsSuppressed=true.
/// </summary>
public enum MechanicAnalysisStatus
{
    Draft = 0,
    InReview = 1,
    Published = 2,
    Rejected = 3
}
