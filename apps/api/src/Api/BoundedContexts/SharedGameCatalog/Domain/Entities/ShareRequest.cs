using System.ComponentModel.DataAnnotations;
using Api.BoundedContexts.SharedGameCatalog.Domain.Events;
using Api.BoundedContexts.SharedGameCatalog.Domain.Exceptions;
using Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;
using Api.SharedKernel.Domain.Entities;

namespace Api.BoundedContexts.SharedGameCatalog.Domain.Entities;

/// <summary>
/// Aggregate root representing a request to share a game from a user's library
/// to the shared catalog. Manages the review workflow and document attachments.
/// </summary>
public sealed class ShareRequest : AggregateRoot<Guid>
{
    /// <summary>
    /// Default lock duration for review in minutes.
    /// Issue #2729: This constant serves as a fallback. Actual duration is configured via
    /// SystemConfiguration key "ReviewLock:DefaultDurationMinutes".
    /// </summary>
    public const int DefaultLockDurationMinutes = 30;

    /// <summary>
    /// Maximum number of documents that can be attached to a share request.
    /// </summary>
    public const int MaxDocumentCount = 10;

    private Guid _id;
    private Guid _userId;
    private Guid _sourceGameId;
    private Guid? _targetSharedGameId;
    private Guid? _sourcePrivateGameId;
    private ShareRequestStatus _status;
    private ShareRequestStatus? _statusBeforeReview;
    private readonly ContributionType _contributionType;
    private string? _userNotes;
    private string? _adminFeedback;
    private Guid? _reviewingAdminId;
    private DateTime? _reviewStartedAt;
    private DateTime? _reviewLockExpiresAt;
    private DateTime? _resolvedAt;
    private readonly DateTime _createdAt;
    private DateTime? _modifiedAt;
    private Guid _createdBy;
    private Guid? _modifiedBy;

    private readonly List<ShareRequestDocument> _attachedDocuments = new();

    /// <summary>
    /// Gets the unique identifier of this share request.
    /// </summary>
    public new Guid Id => _id;

    /// <summary>
    /// Gets the ID of the user who created this request.
    /// </summary>
    public Guid UserId => _userId;

    /// <summary>
    /// Gets the ID of the source game from the user's library.
    /// </summary>
    public Guid SourceGameId => _sourceGameId;

    /// <summary>
    /// Gets the ID of the target shared game (if contributing to existing game).
    /// Null for new game contributions.
    /// </summary>
    public Guid? TargetSharedGameId => _targetSharedGameId;

    /// <summary>
    /// Gets the ID of the source private game (for NewGameProposal contributions).
    /// Null for other contribution types.
    /// Issue #3665: Added for Phase 4 - Proposal System.
    /// </summary>
    public Guid? SourcePrivateGameId => _sourcePrivateGameId;

    /// <summary>
    /// Gets the current status of this share request.
    /// </summary>
    public ShareRequestStatus Status => _status;

    /// <summary>
    /// Gets the status before the current review started.
    /// Used to return to the correct state when review is released.
    /// </summary>
    public ShareRequestStatus? StatusBeforeReview => _statusBeforeReview;

    /// <summary>
    /// Gets the type of contribution (new game or additional content).
    /// </summary>
    public ContributionType ContributionType => _contributionType;

    /// <summary>
    /// Gets optional notes provided by the user.
    /// </summary>
    public string? UserNotes => _userNotes;

    /// <summary>
    /// Gets feedback provided by the reviewing admin.
    /// </summary>
    public string? AdminFeedback => _adminFeedback;

    /// <summary>
    /// Gets the ID of the admin currently reviewing this request.
    /// Null if not under review.
    /// </summary>
    public Guid? ReviewingAdminId => _reviewingAdminId;

    /// <summary>
    /// Gets the date and time when review started.
    /// Null if not under review.
    /// </summary>
    public DateTime? ReviewStartedAt => _reviewStartedAt;

    /// <summary>
    /// Gets the date and time when the review lock expires.
    /// Null if not under review.
    /// </summary>
    public DateTime? ReviewLockExpiresAt => _reviewLockExpiresAt;

    /// <summary>
    /// Gets the date and time when this request was resolved (approved/rejected/withdrawn).
    /// Null if still pending or in review.
    /// </summary>
    public DateTime? ResolvedAt => _resolvedAt;

    /// <summary>
    /// Gets the date and time when this request was created.
    /// </summary>
    public DateTime CreatedAt => _createdAt;

    /// <summary>
    /// Gets the date and time when this request was last modified.
    /// </summary>
    public DateTime? ModifiedAt => _modifiedAt;

    /// <summary>
    /// Gets the ID of the user who created this request.
    /// </summary>
    public Guid CreatedBy => _createdBy;

    /// <summary>
    /// Gets the ID of the user who last modified this request.
    /// </summary>
    public Guid? ModifiedBy => _modifiedBy;

    /// <summary>
    /// Gets the collection of documents attached to this request.
    /// </summary>
    public IReadOnlyCollection<ShareRequestDocument> AttachedDocuments => _attachedDocuments.AsReadOnly();

    /// <summary>
    /// Navigation property to the source private game (for NewGameProposal contributions).
    /// Issue #3665: Added for Phase 4 - Proposal System.
    /// </summary>
    public UserLibrary.Domain.Entities.PrivateGame? PrivateGame { get; }

    /// <summary>
    /// Gets or sets the row version for optimistic concurrency control.
    /// </summary>
    [Timestamp]
    public byte[]? RowVersion { get; private set; }

    /// <summary>
    /// Private constructor for EF Core.
    /// </summary>
#pragma warning disable S1144 // Unused private types or members should be removed - Required for EF Core
    private ShareRequest() : base()
    {
        _createdAt = DateTime.UtcNow;
    }
#pragma warning restore S1144

    /// <summary>
    /// Internal constructor for reconstitution from persistence.
    /// </summary>
    internal ShareRequest(
        Guid id,
        Guid userId,
        Guid sourceGameId,
        Guid? targetSharedGameId,
        Guid? sourcePrivateGameId,
        ShareRequestStatus status,
        ShareRequestStatus? statusBeforeReview,
        ContributionType contributionType,
        string? userNotes,
        string? adminFeedback,
        Guid? reviewingAdminId,
        DateTime? reviewStartedAt,
        DateTime? reviewLockExpiresAt,
        DateTime? resolvedAt,
        DateTime createdAt,
        DateTime? modifiedAt,
        Guid createdBy,
        Guid? modifiedBy,
        byte[]? rowVersion = null,
        List<ShareRequestDocument>? attachedDocuments = null) : base(id)
    {
        _id = id;
        _userId = userId;
        _sourceGameId = sourceGameId;
        _targetSharedGameId = targetSharedGameId;
        _sourcePrivateGameId = sourcePrivateGameId;
        _status = status;
        _statusBeforeReview = statusBeforeReview;
        _contributionType = contributionType;
        _userNotes = userNotes;
        _adminFeedback = adminFeedback;
        _reviewingAdminId = reviewingAdminId;
        _reviewStartedAt = reviewStartedAt;
        _reviewLockExpiresAt = reviewLockExpiresAt;
        _resolvedAt = resolvedAt;
        _createdAt = createdAt;
        _modifiedAt = modifiedAt;
        _createdBy = createdBy;
        _modifiedBy = modifiedBy;
        RowVersion = rowVersion;

        if (attachedDocuments != null)
            _attachedDocuments.AddRange(attachedDocuments);
    }

    /// <summary>
    /// Creates a new share request for a game from user's library.
    /// </summary>
    /// <param name="userId">The ID of the user creating the request.</param>
    /// <param name="sourceGameId">The ID of the game in user's library.</param>
    /// <param name="contributionType">Whether this is a new game or additional content.</param>
    /// <param name="userNotes">Optional notes from the user.</param>
    /// <param name="targetSharedGameId">Target game ID if contributing to existing (required for AdditionalContent).</param>
    /// <returns>A new ShareRequest instance.</returns>
    /// <exception cref="ArgumentException">
    /// Thrown when required parameters are invalid or when AdditionalContent type
    /// is specified without a target shared game ID.
    /// </exception>
    public static ShareRequest Create(
        Guid userId,
        Guid sourceGameId,
        ContributionType contributionType,
        string? userNotes = null,
        Guid? targetSharedGameId = null)
    {
        if (userId == Guid.Empty)
            throw new ArgumentException("UserId cannot be empty", nameof(userId));

        if (sourceGameId == Guid.Empty)
            throw new ArgumentException("SourceGameId cannot be empty", nameof(sourceGameId));

        if (contributionType == ContributionType.AdditionalContent && targetSharedGameId == null)
            throw new ArgumentException(
                "TargetSharedGameId is required for AdditionalContent contributions",
                nameof(targetSharedGameId));

        if (contributionType == ContributionType.NewGame && targetSharedGameId != null)
            throw new ArgumentException(
                "TargetSharedGameId should not be specified for NewGame contributions",
                nameof(targetSharedGameId));

        if (userNotes != null && userNotes.Length > 2000)
            throw new ArgumentException("UserNotes cannot exceed 2000 characters", nameof(userNotes));

        var id = Guid.NewGuid();
        var request = new ShareRequest(
            id,
            userId,
            sourceGameId,
            targetSharedGameId,
            null, // sourcePrivateGameId
            ShareRequestStatus.Pending,
            null,
            contributionType,
            userNotes?.Trim(),
            null,
            null,
            null,
            null,
            null,
            DateTime.UtcNow,
            null,
            userId,
            null);

        request.AddDomainEvent(new ShareRequestCreatedEvent(
            id, userId, sourceGameId, contributionType));

        return request;
    }

    /// <summary>
    /// Creates a new share request for proposing a private game to the catalog.
    /// Issue #3665: Added for Phase 4 - Proposal System.
    /// </summary>
    /// <param name="userId">The ID of the user creating the proposal.</param>
    /// <param name="privateGameId">The ID of the private game to propose.</param>
    /// <param name="userNotes">Optional notes from the user explaining why this game should be added.</param>
    /// <returns>A new ShareRequest instance for game proposal.</returns>
    /// <exception cref="ArgumentException">
    /// Thrown when required parameters are invalid.
    /// </exception>
    public static ShareRequest CreateGameProposal(
        Guid userId,
        Guid privateGameId,
        string? userNotes = null)
    {
        if (userId == Guid.Empty)
            throw new ArgumentException("UserId cannot be empty", nameof(userId));

        if (privateGameId == Guid.Empty)
            throw new ArgumentException("PrivateGameId cannot be empty", nameof(privateGameId));

        if (userNotes != null && userNotes.Length > 2000)
            throw new ArgumentException("UserNotes cannot exceed 2000 characters", nameof(userNotes));

        var id = Guid.NewGuid();
        var request = new ShareRequest(
            id,
            userId,
            Guid.Empty, // sourceGameId not used for proposals
            null, // targetSharedGameId
            privateGameId, // sourcePrivateGameId
            ShareRequestStatus.Pending,
            null,
            ContributionType.NewGameProposal,
            userNotes?.Trim(),
            null,
            null,
            null,
            null,
            null,
            DateTime.UtcNow,
            null,
            userId,
            null);

        request.AddDomainEvent(new ShareRequestCreatedEvent(
            id, userId, privateGameId, ContributionType.NewGameProposal));

        return request;
    }

    /// <summary>
    /// Starts the review process by an admin.
    /// Acquires an exclusive lock for the reviewing admin with a time-limited duration.
    /// </summary>
    /// <param name="adminId">The ID of the admin starting the review.</param>
    /// <param name="lockDurationMinutes">Duration of the review lock in minutes (default: 30).</param>
    /// <exception cref="ArgumentException">Thrown when adminId is empty.</exception>
    /// <exception cref="InvalidShareRequestStateException">
    /// Thrown when the request is not in a reviewable state.
    /// </exception>
    /// <exception cref="ShareRequestAlreadyInReviewException">
    /// Thrown when the request is already being reviewed by another admin.
    /// </exception>
    public void StartReview(Guid adminId, int lockDurationMinutes = DefaultLockDurationMinutes)
    {
        if (adminId == Guid.Empty)
            throw new ArgumentException("AdminId cannot be empty", nameof(adminId));

        if (_status != ShareRequestStatus.Pending && _status != ShareRequestStatus.ChangesRequested)
            throw new InvalidShareRequestStateException(
                _id,
                _status,
                "start review",
                ShareRequestStatus.Pending,
                ShareRequestStatus.ChangesRequested);

        if (_reviewingAdminId != null)
            throw new ShareRequestAlreadyInReviewException(
                _id,
                _reviewingAdminId.Value,
                _reviewStartedAt ?? DateTime.UtcNow);

        _statusBeforeReview = _status;
        _status = ShareRequestStatus.InReview;
        _reviewingAdminId = adminId;
        _reviewStartedAt = DateTime.UtcNow;
        _reviewLockExpiresAt = DateTime.UtcNow.AddMinutes(lockDurationMinutes);
        _modifiedAt = DateTime.UtcNow;
        _modifiedBy = adminId;

        AddDomainEvent(new ShareRequestReviewStartedEvent(_id, adminId));
    }

    /// <summary>
    /// Extends the review lock for additional time.
    /// </summary>
    /// <param name="adminId">The ID of the admin extending the lock.</param>
    /// <param name="additionalMinutes">Additional minutes to extend the lock.</param>
    /// <exception cref="InvalidShareRequestStateException">
    /// Thrown when the request is not in review.
    /// </exception>
    /// <exception cref="ShareRequestReviewerMismatchException">
    /// Thrown when the admin is not the current reviewer.
    /// </exception>
    public void ExtendReviewLock(Guid adminId, int additionalMinutes = DefaultLockDurationMinutes)
    {
        if (adminId == Guid.Empty)
            throw new ArgumentException("AdminId cannot be empty", nameof(adminId));

        if (_status != ShareRequestStatus.InReview)
            throw new InvalidShareRequestStateException(
                _id,
                _status,
                "extend review lock",
                ShareRequestStatus.InReview);

        if (_reviewingAdminId != adminId)
            throw new ShareRequestReviewerMismatchException(_id, _reviewingAdminId!.Value, adminId);

        _reviewLockExpiresAt = _reviewLockExpiresAt!.Value.AddMinutes(additionalMinutes);
        _modifiedAt = DateTime.UtcNow;
        _modifiedBy = adminId;

        AddDomainEvent(new ShareRequestLockExtendedEvent(_id, adminId, _reviewLockExpiresAt.Value));
    }

    /// <summary>
    /// Releases the review lock without making a decision.
    /// Returns to the previous state (Pending or ChangesRequested).
    /// </summary>
    /// <exception cref="InvalidShareRequestStateException">
    /// Thrown when the request is not currently in review.
    /// </exception>
    public void ReleaseReview()
    {
        if (_status != ShareRequestStatus.InReview)
            throw new InvalidShareRequestStateException(
                _id,
                _status,
                "release review",
                ShareRequestStatus.InReview);

        var adminId = _reviewingAdminId!.Value;
        _status = _statusBeforeReview ?? ShareRequestStatus.Pending;
        _statusBeforeReview = null;
        _reviewingAdminId = null;
        _reviewStartedAt = null;
        _reviewLockExpiresAt = null;
        _modifiedAt = DateTime.UtcNow;
        _modifiedBy = adminId;

        AddDomainEvent(new ShareRequestReviewReleasedEvent(_id, adminId));
    }

    /// <summary>
    /// Expires the review lock due to timeout.
    /// Called by a background process when the lock has expired.
    /// Returns to the previous state (Pending or ChangesRequested).
    /// </summary>
    /// <exception cref="InvalidShareRequestStateException">
    /// Thrown when the request is not currently in review.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the lock has not yet expired.
    /// </exception>
    public void ExpireLock()
    {
        if (_status != ShareRequestStatus.InReview)
            throw new InvalidShareRequestStateException(
                _id,
                _status,
                "expire lock",
                ShareRequestStatus.InReview);

        if (_reviewLockExpiresAt == null || DateTime.UtcNow < _reviewLockExpiresAt)
            throw new InvalidOperationException(
                "Cannot expire lock that has not yet reached its expiration time.");

        var adminId = _reviewingAdminId!.Value;
        var expiredAt = _reviewLockExpiresAt.Value;
        var returnToStatus = _statusBeforeReview ?? ShareRequestStatus.Pending;

        _status = returnToStatus;
        _statusBeforeReview = null;
        _reviewingAdminId = null;
        _reviewStartedAt = null;
        _reviewLockExpiresAt = null;
        _modifiedAt = DateTime.UtcNow;

        AddDomainEvent(new ShareRequestLockExpiredEvent(_id, adminId, expiredAt, returnToStatus));
    }

    /// <summary>
    /// Checks if the review lock has expired.
    /// </summary>
    /// <returns>True if the lock has expired, false otherwise.</returns>
    public bool IsLockExpired()
    {
        if (_status != ShareRequestStatus.InReview || _reviewLockExpiresAt == null)
            return false;

        return DateTime.UtcNow >= _reviewLockExpiresAt;
    }

    /// <summary>
    /// Approves the share request.
    /// </summary>
    /// <param name="adminId">The ID of the approving admin.</param>
    /// <param name="targetSharedGameId">
    /// The ID of the created/updated shared game (for NewGame contributions,
    /// this should be the newly created game ID).
    /// </param>
    /// <param name="feedback">Optional feedback from the admin.</param>
    /// <exception cref="ArgumentException">Thrown when adminId is empty.</exception>
    /// <exception cref="InvalidShareRequestStateException">
    /// Thrown when the request is not in review.
    /// </exception>
    /// <exception cref="ShareRequestReviewerMismatchException">
    /// Thrown when the admin is not the current reviewer.
    /// </exception>
    /// <exception cref="ShareRequestLockExpiredException">
    /// Thrown when the review lock has expired.
    /// </exception>
    public void Approve(Guid adminId, Guid? targetSharedGameId = null, string? feedback = null)
    {
        if (adminId == Guid.Empty)
            throw new ArgumentException("AdminId cannot be empty", nameof(adminId));

        if (_status != ShareRequestStatus.InReview)
            throw new InvalidShareRequestStateException(
                _id,
                _status,
                "approve",
                ShareRequestStatus.InReview);

        if (_reviewingAdminId != adminId)
            throw new ShareRequestReviewerMismatchException(_id, _reviewingAdminId!.Value, adminId);

        if (IsLockExpired())
            throw new ShareRequestLockExpiredException(_id, adminId, _reviewLockExpiresAt!.Value);

        if (feedback != null && feedback.Length > 2000)
            throw new ArgumentException("Feedback cannot exceed 2000 characters", nameof(feedback));

        _status = ShareRequestStatus.Approved;
        _statusBeforeReview = null;
        _targetSharedGameId = targetSharedGameId ?? _targetSharedGameId;
        _adminFeedback = feedback?.Trim();
        _resolvedAt = DateTime.UtcNow;
        _reviewLockExpiresAt = null;
        _modifiedAt = DateTime.UtcNow;
        _modifiedBy = adminId;

        AddDomainEvent(new ShareRequestApprovedEvent(_id, adminId, _targetSharedGameId));
    }

    /// <summary>
    /// Rejects the share request.
    /// </summary>
    /// <param name="adminId">The ID of the rejecting admin.</param>
    /// <param name="reason">The reason for rejection (required).</param>
    /// <exception cref="ArgumentException">
    /// Thrown when adminId is empty or reason is not provided.
    /// </exception>
    /// <exception cref="InvalidShareRequestStateException">
    /// Thrown when the request is not in review.
    /// </exception>
    /// <exception cref="ShareRequestReviewerMismatchException">
    /// Thrown when the admin is not the current reviewer.
    /// </exception>
    /// <exception cref="ShareRequestLockExpiredException">
    /// Thrown when the review lock has expired.
    /// </exception>
    public void Reject(Guid adminId, string reason)
    {
        if (adminId == Guid.Empty)
            throw new ArgumentException("AdminId cannot be empty", nameof(adminId));

        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("Rejection reason is required", nameof(reason));

        if (reason.Length > 2000)
            throw new ArgumentException("Reason cannot exceed 2000 characters", nameof(reason));

        if (_status != ShareRequestStatus.InReview)
            throw new InvalidShareRequestStateException(
                _id,
                _status,
                "reject",
                ShareRequestStatus.InReview);

        if (_reviewingAdminId != adminId)
            throw new ShareRequestReviewerMismatchException(_id, _reviewingAdminId!.Value, adminId);

        if (IsLockExpired())
            throw new ShareRequestLockExpiredException(_id, adminId, _reviewLockExpiresAt!.Value);

        _status = ShareRequestStatus.Rejected;
        _statusBeforeReview = null;
        _adminFeedback = reason.Trim();
        _resolvedAt = DateTime.UtcNow;
        _reviewLockExpiresAt = null;
        _modifiedAt = DateTime.UtcNow;
        _modifiedBy = adminId;

        AddDomainEvent(new ShareRequestRejectedEvent(_id, adminId, reason));
    }

    /// <summary>
    /// Requests changes from the user before approval.
    /// </summary>
    /// <param name="adminId">The ID of the admin requesting changes.</param>
    /// <param name="feedback">Detailed feedback about required changes.</param>
    /// <exception cref="ArgumentException">
    /// Thrown when adminId is empty or feedback is not provided.
    /// </exception>
    /// <exception cref="InvalidShareRequestStateException">
    /// Thrown when the request is not in review.
    /// </exception>
    /// <exception cref="ShareRequestReviewerMismatchException">
    /// Thrown when the admin is not the current reviewer.
    /// </exception>
    /// <exception cref="ShareRequestLockExpiredException">
    /// Thrown when the review lock has expired.
    /// </exception>
    public void RequestChanges(Guid adminId, string feedback)
    {
        if (adminId == Guid.Empty)
            throw new ArgumentException("AdminId cannot be empty", nameof(adminId));

        if (string.IsNullOrWhiteSpace(feedback))
            throw new ArgumentException("Feedback is required when requesting changes", nameof(feedback));

        if (feedback.Length > 2000)
            throw new ArgumentException("Feedback cannot exceed 2000 characters", nameof(feedback));

        if (_status != ShareRequestStatus.InReview)
            throw new InvalidShareRequestStateException(
                _id,
                _status,
                "request changes",
                ShareRequestStatus.InReview);

        if (_reviewingAdminId != adminId)
            throw new ShareRequestReviewerMismatchException(_id, _reviewingAdminId!.Value, adminId);

        if (IsLockExpired())
            throw new ShareRequestLockExpiredException(_id, adminId, _reviewLockExpiresAt!.Value);

        _status = ShareRequestStatus.ChangesRequested;
        _statusBeforeReview = null;
        _adminFeedback = feedback.Trim();
        _reviewingAdminId = null;
        _reviewStartedAt = null;
        _reviewLockExpiresAt = null;
        _modifiedAt = DateTime.UtcNow;
        _modifiedBy = adminId;

        AddDomainEvent(new ShareRequestChangesRequestedEvent(_id, adminId, feedback));
    }

    /// <summary>
    /// Resubmits the request after making requested changes.
    /// </summary>
    /// <param name="updatedNotes">Optional updated notes from the user.</param>
    /// <exception cref="InvalidShareRequestStateException">
    /// Thrown when the request is not in ChangesRequested status.
    /// </exception>
    public void Resubmit(string? updatedNotes = null)
    {
        if (_status != ShareRequestStatus.ChangesRequested)
            throw new InvalidShareRequestStateException(
                _id,
                _status,
                "resubmit",
                ShareRequestStatus.ChangesRequested);

        if (updatedNotes != null && updatedNotes.Length > 2000)
            throw new ArgumentException("UserNotes cannot exceed 2000 characters", nameof(updatedNotes));

        _status = ShareRequestStatus.Pending;
        if (updatedNotes != null)
            _userNotes = updatedNotes.Trim();
        _modifiedAt = DateTime.UtcNow;
        _modifiedBy = _userId;

        AddDomainEvent(new ShareRequestResubmittedEvent(_id));
    }

    /// <summary>
    /// Withdraws the share request.
    /// Only pending or changes-requested requests can be withdrawn.
    /// </summary>
    /// <exception cref="InvalidShareRequestStateException">
    /// Thrown when the request cannot be withdrawn in its current state.
    /// </exception>
    public void Withdraw()
    {
        if (_status != ShareRequestStatus.Pending && _status != ShareRequestStatus.ChangesRequested)
            throw new InvalidShareRequestStateException(
                _id,
                _status,
                "withdraw",
                ShareRequestStatus.Pending,
                ShareRequestStatus.ChangesRequested);

        _status = ShareRequestStatus.Withdrawn;
        _resolvedAt = DateTime.UtcNow;
        _modifiedAt = DateTime.UtcNow;
        _modifiedBy = _userId;

        AddDomainEvent(new ShareRequestWithdrawnEvent(_id));
    }

    /// <summary>
    /// Attaches a document to this share request.
    /// </summary>
    /// <param name="documentId">The ID of the document in DocumentProcessing.</param>
    /// <param name="fileName">The original file name.</param>
    /// <param name="contentType">The MIME content type.</param>
    /// <param name="fileSize">The file size in bytes.</param>
    /// <exception cref="InvalidShareRequestStateException">
    /// Thrown when documents cannot be attached in the current state.
    /// </exception>
    /// <exception cref="ShareRequestDocumentLimitExceededException">
    /// Thrown when the maximum document count would be exceeded.
    /// </exception>
    public void AttachDocument(Guid documentId, string fileName, string contentType, long fileSize)
    {
        if (_status != ShareRequestStatus.Pending && _status != ShareRequestStatus.ChangesRequested)
            throw new InvalidShareRequestStateException(
                _id,
                _status,
                "attach documents",
                ShareRequestStatus.Pending,
                ShareRequestStatus.ChangesRequested);

        if (_attachedDocuments.Count >= MaxDocumentCount)
            throw new ShareRequestDocumentLimitExceededException(_id, _attachedDocuments.Count, MaxDocumentCount);

        var document = ShareRequestDocument.Create(_id, documentId, fileName, contentType, fileSize);
        _attachedDocuments.Add(document);
        _modifiedAt = DateTime.UtcNow;
        _modifiedBy = _userId;
    }

    /// <summary>
    /// Removes a document from this share request.
    /// </summary>
    /// <param name="documentId">The ID of the document to remove.</param>
    /// <exception cref="InvalidShareRequestStateException">
    /// Thrown when documents cannot be removed in the current state.
    /// </exception>
    public void RemoveDocument(Guid documentId)
    {
        if (_status != ShareRequestStatus.Pending && _status != ShareRequestStatus.ChangesRequested)
            throw new InvalidShareRequestStateException(
                _id,
                _status,
                "remove documents",
                ShareRequestStatus.Pending,
                ShareRequestStatus.ChangesRequested);

        var document = _attachedDocuments.FirstOrDefault(d => d.DocumentId == documentId);
        if (document != null)
        {
            _attachedDocuments.Remove(document);
            _modifiedAt = DateTime.UtcNow;
            _modifiedBy = _userId;
        }
    }

    /// <summary>
    /// Checks if the request can be reviewed.
    /// </summary>
    public bool CanBeReviewed() =>
        _status == ShareRequestStatus.Pending || _status == ShareRequestStatus.ChangesRequested;

    /// <summary>
    /// Checks if the request is currently locked by an admin.
    /// </summary>
    public bool IsLockedForReview() =>
        _status == ShareRequestStatus.InReview && _reviewingAdminId != null;

    /// <summary>
    /// Checks if the request has been resolved (approved, rejected, or withdrawn).
    /// </summary>
    public bool IsResolved() =>
        _status == ShareRequestStatus.Approved ||
        _status == ShareRequestStatus.Rejected ||
        _status == ShareRequestStatus.Withdrawn;

    /// <summary>
    /// Gets the remaining time on the review lock.
    /// </summary>
    /// <returns>The remaining time, or null if not locked or expired.</returns>
    public TimeSpan? GetRemainingLockTime()
    {
        if (_status != ShareRequestStatus.InReview || _reviewLockExpiresAt == null)
            return null;

        var remaining = _reviewLockExpiresAt.Value - DateTime.UtcNow;
        return remaining > TimeSpan.Zero ? remaining : null;
    }
}
