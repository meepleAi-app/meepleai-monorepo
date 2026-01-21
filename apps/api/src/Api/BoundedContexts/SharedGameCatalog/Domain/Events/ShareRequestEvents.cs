using Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;
using Api.SharedKernel.Domain.Events;

namespace Api.BoundedContexts.SharedGameCatalog.Domain.Events;

/// <summary>
/// Domain event raised when a new share request is created.
/// </summary>
internal sealed class ShareRequestCreatedEvent : DomainEventBase
{
    public Guid ShareRequestId { get; }
    public Guid UserId { get; }
    public Guid SourceGameId { get; }
    public ContributionType ContributionType { get; }

    public ShareRequestCreatedEvent(
        Guid shareRequestId,
        Guid userId,
        Guid sourceGameId,
        ContributionType contributionType)
    {
        ShareRequestId = shareRequestId;
        UserId = userId;
        SourceGameId = sourceGameId;
        ContributionType = contributionType;
    }
}

/// <summary>
/// Domain event raised when an admin starts reviewing a share request.
/// </summary>
internal sealed class ShareRequestReviewStartedEvent : DomainEventBase
{
    public Guid ShareRequestId { get; }
    public Guid AdminId { get; }

    public ShareRequestReviewStartedEvent(Guid shareRequestId, Guid adminId)
    {
        ShareRequestId = shareRequestId;
        AdminId = adminId;
    }
}

/// <summary>
/// Domain event raised when an admin releases their review lock on a share request.
/// </summary>
internal sealed class ShareRequestReviewReleasedEvent : DomainEventBase
{
    public Guid ShareRequestId { get; }
    public Guid AdminId { get; }

    public ShareRequestReviewReleasedEvent(Guid shareRequestId, Guid adminId)
    {
        ShareRequestId = shareRequestId;
        AdminId = adminId;
    }
}

/// <summary>
/// Domain event raised when a share request is approved.
/// </summary>
internal sealed class ShareRequestApprovedEvent : DomainEventBase
{
    public Guid ShareRequestId { get; }
    public Guid AdminId { get; }
    public Guid? TargetSharedGameId { get; }

    public ShareRequestApprovedEvent(
        Guid shareRequestId,
        Guid adminId,
        Guid? targetSharedGameId)
    {
        ShareRequestId = shareRequestId;
        AdminId = adminId;
        TargetSharedGameId = targetSharedGameId;
    }
}

/// <summary>
/// Domain event raised when a share request is rejected.
/// </summary>
internal sealed class ShareRequestRejectedEvent : DomainEventBase
{
    public Guid ShareRequestId { get; }
    public Guid AdminId { get; }
    public string Reason { get; }

    public ShareRequestRejectedEvent(
        Guid shareRequestId,
        Guid adminId,
        string reason)
    {
        ShareRequestId = shareRequestId;
        AdminId = adminId;
        Reason = reason;
    }
}

/// <summary>
/// Domain event raised when an admin requests changes to a share request.
/// </summary>
internal sealed class ShareRequestChangesRequestedEvent : DomainEventBase
{
    public Guid ShareRequestId { get; }
    public Guid AdminId { get; }
    public string Feedback { get; }

    public ShareRequestChangesRequestedEvent(
        Guid shareRequestId,
        Guid adminId,
        string feedback)
    {
        ShareRequestId = shareRequestId;
        AdminId = adminId;
        Feedback = feedback;
    }
}

/// <summary>
/// Domain event raised when a user resubmits a share request after changes were requested.
/// </summary>
internal sealed class ShareRequestResubmittedEvent : DomainEventBase
{
    public Guid ShareRequestId { get; }

    public ShareRequestResubmittedEvent(Guid shareRequestId)
    {
        ShareRequestId = shareRequestId;
    }
}

/// <summary>
/// Domain event raised when a user withdraws their share request.
/// </summary>
internal sealed class ShareRequestWithdrawnEvent : DomainEventBase
{
    public Guid ShareRequestId { get; }

    public ShareRequestWithdrawnEvent(Guid shareRequestId)
    {
        ShareRequestId = shareRequestId;
    }
}
