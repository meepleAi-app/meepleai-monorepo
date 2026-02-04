namespace Api.BoundedContexts.SharedGameCatalog.Domain.Entities;

/// <summary>
/// Approval status for documents requiring admin review before RAG processing.
/// </summary>
public enum DocumentApprovalStatus
{
    /// <summary>
    /// Document is awaiting admin approval.
    /// </summary>
    Pending = 0,

    /// <summary>
    /// Document has been approved and can be processed for RAG.
    /// </summary>
    Approved = 1,

    /// <summary>
    /// Document has been rejected and will not be processed.
    /// </summary>
    Rejected = 2
}
