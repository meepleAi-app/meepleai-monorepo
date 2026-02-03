namespace Api.BoundedContexts.GameManagement.Domain.ValueObjects;

/// <summary>
/// Issue #3481: Publication approval workflow status.
/// Represents the approval state of a game in SharedGameCatalog.
/// </summary>
public enum ApprovalStatus
{
    /// <summary>
    /// Game is in draft state, not submitted for review.
    /// </summary>
    Draft = 0,

    /// <summary>
    /// Game has been submitted and is awaiting admin review.
    /// </summary>
    PendingReview = 1,

    /// <summary>
    /// Game has been approved and published to SharedGameCatalog.
    /// </summary>
    Approved = 2,

    /// <summary>
    /// Game submission was rejected and requires revisions.
    /// </summary>
    Rejected = 3
}
