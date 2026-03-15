namespace Api.Infrastructure.Entities.UserNotifications;

public class NotificationPreferencesEntity
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public bool EmailOnDocumentReady { get; set; } = true;
    public bool EmailOnDocumentFailed { get; set; } = true;
    public bool EmailOnRetryAvailable { get; set; }
    public bool PushOnDocumentReady { get; set; } = true;
    public bool PushOnDocumentFailed { get; set; } = true;
    public bool PushOnRetryAvailable { get; set; }
    public string? PushEndpoint { get; set; }
    public string? PushP256dhKey { get; set; }
    public string? PushAuthKey { get; set; }
    public bool InAppOnDocumentReady { get; set; } = true;
    public bool InAppOnDocumentFailed { get; set; } = true;
    public bool InAppOnRetryAvailable { get; set; } = true;

    // Game Night - Issue #44/#47
    public bool InAppOnGameNightInvitation { get; set; } = true;
    public bool EmailOnGameNightInvitation { get; set; } = true;
    public bool PushOnGameNightInvitation { get; set; } = true;
    public bool EmailOnGameNightReminder { get; set; } = true;
    public bool PushOnGameNightReminder { get; set; } = true;

    // Slack - Issue #slack-notification-system
    public bool SlackEnabled { get; set; } = true;
    public bool SlackOnDocumentReady { get; set; } = true;
    public bool SlackOnDocumentFailed { get; set; } = true;
    public bool SlackOnRetryAvailable { get; set; }
    public bool SlackOnGameNightInvitation { get; set; } = true;
    public bool SlackOnGameNightReminder { get; set; } = true;
    public bool SlackOnShareRequestCreated { get; set; } = true;
    public bool SlackOnShareRequestApproved { get; set; } = true;
    public bool SlackOnBadgeEarned { get; set; } = true;
}
