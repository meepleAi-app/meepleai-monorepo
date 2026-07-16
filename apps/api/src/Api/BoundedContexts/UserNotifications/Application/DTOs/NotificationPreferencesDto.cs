namespace Api.BoundedContexts.UserNotifications.Application.DTOs;

/// <summary>
/// User notification preferences response.
/// Issue #4220: Multi-channel notification configuration.
/// Issue #2994: surfaces quiet-hours (ADR-076) and Slack channel preferences so the FE
/// Preferences UI can round-trip them (previously modelled BE-side but not exposed).
/// </summary>
internal record NotificationPreferencesDto(
    Guid UserId,
    bool EmailOnDocumentReady,
    bool EmailOnDocumentFailed,
    bool EmailOnRetryAvailable,
    bool PushOnDocumentReady,
    bool PushOnDocumentFailed,
    bool PushOnRetryAvailable,
    bool InAppOnDocumentReady,
    bool InAppOnDocumentFailed,
    bool InAppOnRetryAvailable,
    bool HasPushSubscription,
    bool EmailOnCardSuppressed, // #535/#2832: opt-in admin email on mechanic-card suppression

    // Quiet hours (ADR-076 / #2995). Times are "HH:mm" strings; null when not configured.
    string TimeZone,
    string? QuietHoursStart,
    string? QuietHoursEnd,

    // Slack channel preferences (written via PUT /notifications/preferences/slack).
    bool SlackEnabled,
    bool SlackOnDocumentReady,
    bool SlackOnDocumentFailed,
    bool SlackOnRetryAvailable,
    bool SlackOnGameNightInvitation,
    bool SlackOnGameNightReminder,
    bool SlackOnShareRequestCreated,
    bool SlackOnShareRequestApproved,
    bool SlackOnBadgeEarned
);
