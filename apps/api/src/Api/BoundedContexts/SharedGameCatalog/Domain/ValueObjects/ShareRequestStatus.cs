namespace Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;

/// <summary>
/// Represents the status of a share request in the review workflow.
/// </summary>
public enum ShareRequestStatus
{
    /// <summary>
    /// Request is pending admin review.
    /// Initial state after creation.
    /// </summary>
    Pending = 0,

    /// <summary>
    /// Request is currently being reviewed by an admin.
    /// Locked for exclusive review.
    /// </summary>
    InReview = 1,

    /// <summary>
    /// Admin has requested changes from the user.
    /// User can resubmit with modifications.
    /// </summary>
    ChangesRequested = 2,

    /// <summary>
    /// Request has been approved by admin.
    /// Game will be added to shared catalog.
    /// </summary>
    Approved = 3,

    /// <summary>
    /// Request has been rejected by admin.
    /// Terminal state.
    /// </summary>
    Rejected = 4,

    /// <summary>
    /// Request has been withdrawn by the user.
    /// Terminal state.
    /// </summary>
    Withdrawn = 5
}
