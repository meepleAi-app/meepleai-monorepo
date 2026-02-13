using Api.SharedKernel.Domain.ValueObjects;

namespace Api.BoundedContexts.UserNotifications.Domain.ValueObjects;

/// <summary>
/// Value object representing notification type.
/// Defines event types that trigger user notifications.
/// </summary>
internal sealed class NotificationType : ValueObject
{
    public string Value { get; }

    public static readonly NotificationType PdfUploadCompleted = new("pdf_upload_completed");
    public static readonly NotificationType RuleSpecGenerated = new("rule_spec_generated");
    public static readonly NotificationType ProcessingFailed = new("processing_failed");
    public static readonly NotificationType NewComment = new("new_comment");
    public static readonly NotificationType SharedLinkAccessed = new("shared_link_accessed");

    // ISSUE-2739: Share request notification types
    public static readonly NotificationType ShareRequestCreated = new("share_request_created");
    public static readonly NotificationType ShareRequestApproved = new("share_request_approved");
    public static readonly NotificationType ShareRequestRejected = new("share_request_rejected");
    public static readonly NotificationType ShareRequestChangesRequested = new("share_request_changes_requested");

    // ISSUE-2740: Admin notification types for share request management
    public static readonly NotificationType AdminNewShareRequest = new("admin_new_share_request");
    public static readonly NotificationType AdminStaleShareRequests = new("admin_stale_share_requests");
    public static readonly NotificationType AdminReviewLockExpiring = new("admin_review_lock_expiring");

    // ISSUE-4159: Admin notification for shared game submission (PDF Wizard)
    public static readonly NotificationType AdminSharedGameSubmitted = new("admin_shared_game_submitted");

    // ISSUE-2741: Badge earned notification type
    public static readonly NotificationType BadgeEarned = new("badge_earned");

    // ISSUE-2742: Rate limit warning notification types
    public static readonly NotificationType RateLimitApproaching = new("rate_limit_approaching");
    public static readonly NotificationType RateLimitReached = new("rate_limit_reached");
    public static readonly NotificationType CooldownEnded = new("cooldown_ended");

    // ISSUE-2830: Loan reminder notification type
    public static readonly NotificationType LoanReminder = new("loan_reminder");

    // ISSUE-3671: Session quota enforcement notification types
    public static readonly NotificationType SessionTerminated = new("session_terminated");

    // ISSUE-3668: Game proposal lifecycle notification types
    public static readonly NotificationType GameProposalInReview = new("game_proposal_in_review");
    public static readonly NotificationType GameProposalKbMerged = new("game_proposal_kb_merged");

    private NotificationType(string value)
    {
        Value = value;
    }

    public bool IsPdfUploadCompleted => string.Equals(Value, PdfUploadCompleted.Value, StringComparison.Ordinal);
    public bool IsRuleSpecGenerated => string.Equals(Value, RuleSpecGenerated.Value, StringComparison.Ordinal);
    public bool IsProcessingFailed => string.Equals(Value, ProcessingFailed.Value, StringComparison.Ordinal);

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }

    public override string ToString() => Value;

    /// <summary>
    /// Creates a NotificationType from a string value.
    /// Validates against known types.
    /// </summary>
    public static NotificationType FromString(string value)
    {
        var normalized = value.ToLowerInvariant();
        return normalized switch
        {
            "pdf_upload_completed" => PdfUploadCompleted,
            "rule_spec_generated" => RuleSpecGenerated,
            "processing_failed" => ProcessingFailed,
            "new_comment" => NewComment,
            "shared_link_accessed" => SharedLinkAccessed,
            "share_request_created" => ShareRequestCreated,
            "share_request_approved" => ShareRequestApproved,
            "share_request_rejected" => ShareRequestRejected,
            "share_request_changes_requested" => ShareRequestChangesRequested,
            "admin_new_share_request" => AdminNewShareRequest,
            "admin_stale_share_requests" => AdminStaleShareRequests,
            "admin_review_lock_expiring" => AdminReviewLockExpiring,
            "admin_shared_game_submitted" => AdminSharedGameSubmitted,
            "badge_earned" => BadgeEarned,
            "rate_limit_approaching" => RateLimitApproaching,
            "rate_limit_reached" => RateLimitReached,
            "cooldown_ended" => CooldownEnded,
            "loan_reminder" => LoanReminder,
            "session_terminated" => SessionTerminated,
            "game_proposal_in_review" => GameProposalInReview,
            "game_proposal_kb_merged" => GameProposalKbMerged,
            _ => throw new ArgumentException($"Unknown notification type: {value}", nameof(value))
        };
    }
}
