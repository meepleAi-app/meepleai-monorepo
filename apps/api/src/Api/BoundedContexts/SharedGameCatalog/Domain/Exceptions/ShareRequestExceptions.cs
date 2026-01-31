using Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;
using Api.SharedKernel.Domain.Exceptions;

namespace Api.BoundedContexts.SharedGameCatalog.Domain.Exceptions;

/// <summary>
/// Exception thrown when an operation is attempted on a ShareRequest
/// that is not valid for its current state.
/// </summary>
public sealed class InvalidShareRequestStateException : DomainException
{
    public Guid ShareRequestId { get; }
    public ShareRequestStatus CurrentStatus { get; }
    public string AttemptedOperation { get; }
    public ShareRequestStatus[] AllowedStatuses { get; }

    public InvalidShareRequestStateException(
        Guid shareRequestId,
        ShareRequestStatus currentStatus,
        string attemptedOperation,
        params ShareRequestStatus[] allowedStatuses)
        : base(BuildMessage(currentStatus, attemptedOperation, allowedStatuses))
    {
        ShareRequestId = shareRequestId;
        CurrentStatus = currentStatus;
        AttemptedOperation = attemptedOperation;
        AllowedStatuses = allowedStatuses;
    }

    private static string BuildMessage(
        ShareRequestStatus currentStatus,
        string operation,
        ShareRequestStatus[] allowedStatuses)
    {
        var allowed = string.Join(" or ", allowedStatuses);
        return $"Cannot {operation} for request in {currentStatus} status. " +
               $"Only {allowed} requests can have this operation performed.";
    }
}

/// <summary>
/// Exception thrown when attempting to start review on a ShareRequest
/// that is already being reviewed by another admin.
/// </summary>
public sealed class ShareRequestAlreadyInReviewException : DomainException
{
    public Guid ShareRequestId { get; }
    public Guid CurrentReviewerAdminId { get; }
    public DateTime ReviewStartedAt { get; }

    public ShareRequestAlreadyInReviewException(
        Guid shareRequestId,
        Guid currentReviewerAdminId,
        DateTime reviewStartedAt)
        : base($"Share request {shareRequestId} is already being reviewed by admin {currentReviewerAdminId} since {reviewStartedAt:u}.")
    {
        ShareRequestId = shareRequestId;
        CurrentReviewerAdminId = currentReviewerAdminId;
        ReviewStartedAt = reviewStartedAt;
    }
}

/// <summary>
/// Exception thrown when an admin tries to perform an action on a ShareRequest
/// that is being reviewed by a different admin.
/// </summary>
public sealed class ShareRequestReviewerMismatchException : DomainException
{
    public Guid ShareRequestId { get; }
    public Guid ExpectedAdminId { get; }
    public Guid ActualAdminId { get; }

    public ShareRequestReviewerMismatchException(
        Guid shareRequestId,
        Guid expectedAdminId,
        Guid actualAdminId)
        : base($"Admin {actualAdminId} cannot perform this action. " +
               $"Only the reviewing admin {expectedAdminId} can perform actions on this request.")
    {
        ShareRequestId = shareRequestId;
        ExpectedAdminId = expectedAdminId;
        ActualAdminId = actualAdminId;
    }
}

/// <summary>
/// Exception thrown when a review lock has expired.
/// </summary>
public sealed class ShareRequestLockExpiredException : DomainException
{
    public Guid ShareRequestId { get; }
    public Guid AdminId { get; }
    public DateTime LockExpiredAt { get; }

    public ShareRequestLockExpiredException(
        Guid shareRequestId,
        Guid adminId,
        DateTime lockExpiredAt)
        : base($"Review lock for share request {shareRequestId} held by admin {adminId} " +
               $"has expired at {lockExpiredAt:u}.")
    {
        ShareRequestId = shareRequestId;
        AdminId = adminId;
        LockExpiredAt = lockExpiredAt;
    }
}

/// <summary>
/// Exception thrown when attempting to attach too many documents to a ShareRequest.
/// </summary>
public sealed class ShareRequestDocumentLimitExceededException : DomainException
{
    public Guid ShareRequestId { get; }
    public int CurrentCount { get; }
    public int MaxAllowed { get; }

    public ShareRequestDocumentLimitExceededException(
        Guid shareRequestId,
        int currentCount,
        int maxAllowed)
        : base($"Share request {shareRequestId} cannot have more than {maxAllowed} documents. " +
               $"Current count: {currentCount}.")
    {
        ShareRequestId = shareRequestId;
        CurrentCount = currentCount;
        MaxAllowed = maxAllowed;
    }
}
