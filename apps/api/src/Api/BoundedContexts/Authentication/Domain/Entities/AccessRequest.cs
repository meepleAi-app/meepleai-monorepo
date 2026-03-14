using Api.BoundedContexts.Authentication.Domain.Enums;
using Api.BoundedContexts.Authentication.Domain.Events;
using Api.SharedKernel.Domain.Entities;

namespace Api.BoundedContexts.Authentication.Domain.Entities;

/// <summary>
/// Aggregate root representing a user's request for access to the platform.
/// Tracks the access request lifecycle: pending, approved, or rejected.
/// </summary>
internal sealed class AccessRequest : AggregateRoot<Guid>
{
    public string Email { get; private set; } = null!;
    public AccessRequestStatus Status { get; private set; }
    public DateTime RequestedAt { get; private set; }
    public DateTime? ReviewedAt { get; private set; }
    public Guid? ReviewedBy { get; private set; }
    public string? RejectionReason { get; private set; }
    public Guid? InvitationId { get; private set; }

    // EF Core constructor
    private AccessRequest() { }
    private AccessRequest(Guid id) : base(id) { }

    /// <summary>
    /// Internal constructor for repository materialization (avoids reflection).
    /// </summary>
    internal static AccessRequest CreateForHydration(Guid id) => new(id);

    /// <summary>
    /// Factory method to create a new access request.
    /// </summary>
    /// <param name="email">Email address of the requesting user</param>
    /// <returns>New AccessRequest instance in Pending status</returns>
    public static AccessRequest Create(string email)
    {
        var request = new AccessRequest
        {
            Id = Guid.NewGuid(),
            Email = email.Trim().ToLowerInvariant(),
            Status = AccessRequestStatus.Pending,
            RequestedAt = DateTime.UtcNow
        };

        request.AddDomainEvent(new AccessRequestCreatedEvent(request.Id, request.Email));
        return request;
    }

    /// <summary>
    /// Approves the access request, allowing the user to be invited.
    /// </summary>
    /// <param name="adminId">The admin user who approved the request</param>
    public void Approve(Guid adminId)
    {
        if (Status != AccessRequestStatus.Pending)
            throw new InvalidOperationException(
                $"Cannot approve access request in '{Status}' status. Only Pending requests can be approved.");

        Status = AccessRequestStatus.Approved;
        ReviewedAt = DateTime.UtcNow;
        ReviewedBy = adminId;

        AddDomainEvent(new AccessRequestApprovedEvent(Id, Email, adminId));
    }

    /// <summary>
    /// Rejects the access request with an optional reason.
    /// </summary>
    /// <param name="adminId">The admin user who rejected the request</param>
    /// <param name="reason">Optional reason for rejection (max 500 characters)</param>
    public void Reject(Guid adminId, string? reason = null)
    {
        if (Status != AccessRequestStatus.Pending)
            throw new InvalidOperationException(
                $"Cannot reject access request in '{Status}' status. Only Pending requests can be rejected.");

        if (reason is not null && reason.Length > 500)
            throw new ArgumentException("Rejection reason cannot exceed 500 characters.", nameof(reason));

        Status = AccessRequestStatus.Rejected;
        ReviewedAt = DateTime.UtcNow;
        ReviewedBy = adminId;
        RejectionReason = reason;
    }

    /// <summary>
    /// Associates an invitation token ID with this access request for correlation.
    /// </summary>
    /// <param name="invitationId">The ID of the created invitation token</param>
    public void SetInvitationId(Guid invitationId)
    {
        InvitationId = invitationId;
    }

    #region Persistence Hydration Methods (internal - S3011 fix)

    /// <summary>
    /// Restores access request state from persistence layer.
    /// Internal method to avoid reflection in repository (S3011 compliance).
    /// Should only be called by AccessRequestRepository during entity materialization.
    /// </summary>
    internal void RestoreState(
        string email,
        AccessRequestStatus status,
        DateTime requestedAt,
        DateTime? reviewedAt,
        Guid? reviewedBy,
        string? rejectionReason,
        Guid? invitationId)
    {
        Email = email;
        Status = status;
        RequestedAt = requestedAt;
        ReviewedAt = reviewedAt;
        ReviewedBy = reviewedBy;
        RejectionReason = rejectionReason;
        InvitationId = invitationId;
    }

    #endregion
}
