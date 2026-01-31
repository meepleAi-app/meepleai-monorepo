namespace Api.BoundedContexts.SharedGameCatalog.Domain.Entities;

/// <summary>
/// Represents the status of a delete request.
/// </summary>
public enum DeleteRequestStatus
{
    /// <summary>
    /// Delete request is pending admin approval.
    /// </summary>
    Pending = 0,

    /// <summary>
    /// Delete request has been approved by admin.
    /// </summary>
    Approved = 1,

    /// <summary>
    /// Delete request has been rejected by admin.
    /// </summary>
    Rejected = 2
}
