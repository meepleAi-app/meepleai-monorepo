namespace Api.BoundedContexts.SharedGameCatalog.Domain.Enums;

/// <summary>
/// Lifecycle status of a MechanicAnalysis.
/// Transitions (see <see cref="Aggregates.MechanicAnalysis"/> state machine):
///   Draft (0) → InReview (1):              SubmitForReview
///   Draft (0) → Rejected (3):              AutoRejectFromDraft (system, full abort, no claims salvaged)
///   Draft (0) → PartiallyExtracted (4):    MarkAsPartiallyExtracted (system, partial abort with surviving claims — ADR-051 Sprint 2)
///   InReview (1) → Published (2):          Approve (all claims approved)
///   InReview (1) → Rejected (3):           Reject
///   Rejected (3) → InReview (1):           SubmitForReview (after edits)
///   PartiallyExtracted (4) → InReview (1): SubmitForReview (admin promotes salvage to review)
///   PartiallyExtracted (4) → Rejected (3): Reject (admin discards salvage)
/// Note: Suppressed is NOT a Status value — suppression is orthogonal via <see cref="Aggregates.MechanicAnalysis.IsSuppressed"/>.
/// A Published analysis under takedown stays Published + IsSuppressed=true.
/// </summary>
public enum MechanicAnalysisStatus
{
    Draft = 0,
    InReview = 1,
    Published = 2,
    Rejected = 3,

    /// <summary>
    /// System-initiated checkpoint: the M1.2 generation pipeline aborted mid-run (cost cap, LLM
    /// failure, validation beyond retry) but at least one section was successfully parsed before
    /// the abort. Distinct from <see cref="Rejected"/> so admins can triage what survived rather
    /// than re-running the whole pipeline. Added in ADR-051 Sprint 2 (relaxes the executor's
    /// original all-or-nothing atomicity invariant).
    /// </summary>
    PartiallyExtracted = 4
}
