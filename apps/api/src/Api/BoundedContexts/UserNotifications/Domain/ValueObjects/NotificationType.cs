using Api.SharedKernel.Domain.ValueObjects;

namespace Api.BoundedContexts.UserNotifications.Domain.ValueObjects;

/// <summary>
/// Value object representing notification type.
/// Defines event types that trigger user notifications.
/// </summary>
internal sealed class NotificationType : ValueObject
{
    public string Value { get; }

    public static readonly NotificationType DocumentReady = new("document_ready");
    public static readonly NotificationType RuleSpecGenerated = new("rule_spec_generated");
    public static readonly NotificationType DocumentProcessingFailed = new("document_processing_failed");
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

    // ISSUE-4736: Processing job notification types (merged into DocumentReady / DocumentProcessingFailed)

    // ISSUE-5009: Agent ready notification (merged agent_linked + agent_auto_created)
    public static readonly NotificationType AgentReady = new("agent_ready");

    // ISSUE-5084/5085: OpenRouter threshold alert — RPM and budget (admin)
    public static readonly NotificationType AdminOpenRouterThresholdAlert = new("admin_openrouter_threshold_alert");

    // ISSUE-5085: Daily OpenRouter usage digest sent at 08:00 UTC (admin)
    public static readonly NotificationType AdminOpenRouterDailySummary = new("admin_openrouter_daily_summary");

    // ISSUE-5086/5477: System health alert — circuit breaker + Redis rate-limiting (admin)
    public static readonly NotificationType AdminSystemHealthAlert = new("admin_system_health_alert");

    // ISSUE-5499/5501: Model status changed — deprecated + auto-fallback (admin)
    public static readonly NotificationType AdminModelStatusChanged = new("admin_model_status_changed");

    // Admin notification for new access request (invite-only registration)
    public static readonly NotificationType AdminAccessRequestCreated = new("admin_access_request_created");

    // Admin manual notification sent from compose UI
    public static readonly NotificationType AdminManualNotification = new("admin_manual_notification");

    // Admin notification when PDF processing starts (enriched with uploader details)
    public static readonly NotificationType AdminPdfProcessingStarted = new("admin_pdf_processing_started");

    // ISSUE-44/47: Game night notification types
    public static readonly NotificationType GameNightInvitation = new("game_night_invitation");
    public static readonly NotificationType GameNightRsvpReceived = new("game_night_rsvp_received");
    public static readonly NotificationType GameNightReminder = new("game_night_reminder");
    public static readonly NotificationType GameNightCancelled = new("game_night_cancelled");

    // GDPR Art. 17: Account deletion confirmation
    public static readonly NotificationType GdprAccountDeleted = new("gdpr_account_deleted");

    // GDPR Art. 20: Data export completed
    public static readonly NotificationType GdprDataExportReady = new("gdpr_data_export_ready");

    // GDPR Art. 7: AI consent updated
    public static readonly NotificationType GdprAiConsentUpdated = new("gdpr_ai_consent_updated");

    // Slack connection forcibly revoked by workspace admin
    public static readonly NotificationType SlackConnectionRevoked = new("slack_connection_revoked");

    // ISSUE-524 / M1.2: Mechanic analysis pipeline lifecycle (async pipeline completion alerts).
    // MechanicAnalysisReady fires when the aggregate transitions to InReview (pipeline succeeded).
    // MechanicAnalysisRejected fires when the aggregate is auto-rejected mid-pipeline (e.g. cost cap).
    public static readonly NotificationType MechanicAnalysisReady = new("mechanic_analysis_ready");
    public static readonly NotificationType MechanicAnalysisRejected = new("mechanic_analysis_rejected");

    private NotificationType(string value)
    {
        Value = value;
    }

    public bool IsDocumentReady => string.Equals(Value, DocumentReady.Value, StringComparison.Ordinal);
    public bool IsRuleSpecGenerated => string.Equals(Value, RuleSpecGenerated.Value, StringComparison.Ordinal);
    public bool IsDocumentProcessingFailed => string.Equals(Value, DocumentProcessingFailed.Value, StringComparison.Ordinal);
    public bool IsGameNightInvitation => string.Equals(Value, GameNightInvitation.Value, StringComparison.Ordinal);
    public bool IsGameNightRsvpReceived => string.Equals(Value, GameNightRsvpReceived.Value, StringComparison.Ordinal);
    public bool IsGameNightReminder => string.Equals(Value, GameNightReminder.Value, StringComparison.Ordinal);
    public bool IsGameNightCancelled => string.Equals(Value, GameNightCancelled.Value, StringComparison.Ordinal);
    public bool IsSlackConnectionRevoked => string.Equals(Value, SlackConnectionRevoked.Value, StringComparison.Ordinal);

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
            // Current types
            "document_ready" => DocumentReady,
            "rule_spec_generated" => RuleSpecGenerated,
            "document_processing_failed" => DocumentProcessingFailed,
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
            "agent_ready" => AgentReady,
            "admin_openrouter_threshold_alert" => AdminOpenRouterThresholdAlert,
            "admin_openrouter_daily_summary" => AdminOpenRouterDailySummary,
            "admin_system_health_alert" => AdminSystemHealthAlert,
            "admin_model_status_changed" => AdminModelStatusChanged,
            "gdpr_account_deleted" => GdprAccountDeleted,
            "gdpr_data_export_ready" => GdprDataExportReady,
            "gdpr_ai_consent_updated" => GdprAiConsentUpdated,
            "game_night_invitation" => GameNightInvitation,
            "game_night_rsvp_received" => GameNightRsvpReceived,
            "game_night_reminder" => GameNightReminder,
            "game_night_cancelled" => GameNightCancelled,
            "admin_access_request_created" => AdminAccessRequestCreated,
            "admin_manual_notification" => AdminManualNotification,
            "admin_pdf_processing_started" => AdminPdfProcessingStarted,
            "slack_connection_revoked" => SlackConnectionRevoked,
            "mechanic_analysis_ready" => MechanicAnalysisReady,
            "mechanic_analysis_rejected" => MechanicAnalysisRejected,
            // Legacy aliases — old type strings from DB or older clients map to new types
            "pdf_upload_completed" => DocumentReady,
            "processing_job_completed" => DocumentReady,
            "processing_failed" => DocumentProcessingFailed,
            "processing_job_failed" => DocumentProcessingFailed,
            "agent_linked" => AgentReady,
            "agent_auto_created" => AgentReady,
            "admin_openrouter_rpm_alert" => AdminOpenRouterThresholdAlert,
            "admin_openrouter_budget_alert" => AdminOpenRouterThresholdAlert,
            "admin_circuit_breaker_state_changed" => AdminSystemHealthAlert,
            "admin_redis_rate_limiting_degraded" => AdminSystemHealthAlert,
            "admin_model_deprecated" => AdminModelStatusChanged,
            "admin_model_auto_fallback" => AdminModelStatusChanged,
            "game_night_reminder_24h" => GameNightReminder,
            "game_night_reminder_1h" => GameNightReminder,
            "new_comment" => SharedLinkAccessed,   // legacy: comment feature removed
            _ => throw new ArgumentException($"Unknown notification type: {value}", nameof(value))
        };
    }
}
