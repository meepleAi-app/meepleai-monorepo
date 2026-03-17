using Api.SharedKernel.Domain.Entities;

namespace Api.BoundedContexts.UserNotifications.Domain.Aggregates;

/// <summary>
/// User notification preferences for multi-channel notifications.
/// Issue #4220: Configurable notification channels per event type.
/// </summary>
internal sealed class NotificationPreferences : AggregateRoot<Guid>
{
    public Guid UserId { get; private set; }

    // PDF Processing - Email
    public bool EmailOnDocumentReady { get; private set; } = true;
    public bool EmailOnDocumentFailed { get; private set; } = true;
    public bool EmailOnRetryAvailable { get; private set; }

    // PDF Processing - Push
    public bool PushOnDocumentReady { get; private set; } = true;
    public bool PushOnDocumentFailed { get; private set; } = true;
    public bool PushOnRetryAvailable { get; private set; }

    // Push Subscription (Web Push API - Issue #4416)
    public string? PushEndpoint { get; private set; }
    public string? PushP256dhKey { get; private set; }
    public string? PushAuthKey { get; private set; }

    // PDF Processing - In-App
    public bool InAppOnDocumentReady { get; private set; } = true;
    public bool InAppOnDocumentFailed { get; private set; } = true;
    public bool InAppOnRetryAvailable { get; private set; } = true;

    // Game Night - Issue #44/#47
    public bool InAppOnGameNightInvitation { get; private set; } = true;
    public bool EmailOnGameNightInvitation { get; private set; } = true;
    public bool PushOnGameNightInvitation { get; private set; } = true;
    public bool EmailOnGameNightReminder { get; private set; } = true;
    public bool PushOnGameNightReminder { get; private set; } = true;

    // Slack - Issue #slack-notification-system
    public bool SlackEnabled { get; private set; } = true;
    public bool SlackOnDocumentReady { get; private set; } = true;
    public bool SlackOnDocumentFailed { get; private set; } = true;
    public bool SlackOnRetryAvailable { get; private set; }
    public bool SlackOnGameNightInvitation { get; private set; } = true;
    public bool SlackOnGameNightReminder { get; private set; } = true;
    public bool SlackOnShareRequestCreated { get; private set; } = true;
    public bool SlackOnShareRequestApproved { get; private set; } = true;
    public bool SlackOnBadgeEarned { get; private set; } = true;

#pragma warning disable CS8618
    private NotificationPreferences() : base() { }
#pragma warning restore CS8618

    public NotificationPreferences(Guid userId) : base(Guid.NewGuid())
    {
        if (userId == Guid.Empty)
            throw new ArgumentException("User ID cannot be empty", nameof(userId));

        UserId = userId;
    }

    public static NotificationPreferences Reconstitute(
        Guid id, Guid userId,
        bool emailReady, bool emailFailed, bool emailRetry,
        bool pushReady, bool pushFailed, bool pushRetry,
        bool inAppReady, bool inAppFailed, bool inAppRetry,
        string? pushEndpoint = null, string? pushP256dhKey = null, string? pushAuthKey = null,
        bool inAppOnGameNightInvitation = true, bool emailOnGameNightInvitation = true,
        bool pushOnGameNightInvitation = true, bool emailOnGameNightReminder = true,
        bool pushOnGameNightReminder = true,
        bool slackEnabled = true, bool slackOnDocumentReady = true, bool slackOnDocumentFailed = true,
        bool slackOnRetryAvailable = false, bool slackOnGameNightInvitation = true,
        bool slackOnGameNightReminder = true, bool slackOnShareRequestCreated = true,
        bool slackOnShareRequestApproved = true, bool slackOnBadgeEarned = true)
    {
        return new NotificationPreferences
        {
            Id = id,
            UserId = userId,
            EmailOnDocumentReady = emailReady,
            EmailOnDocumentFailed = emailFailed,
            EmailOnRetryAvailable = emailRetry,
            PushOnDocumentReady = pushReady,
            PushOnDocumentFailed = pushFailed,
            PushOnRetryAvailable = pushRetry,
            InAppOnDocumentReady = inAppReady,
            InAppOnDocumentFailed = inAppFailed,
            InAppOnRetryAvailable = inAppRetry,
            PushEndpoint = pushEndpoint,
            PushP256dhKey = pushP256dhKey,
            PushAuthKey = pushAuthKey,
            InAppOnGameNightInvitation = inAppOnGameNightInvitation,
            EmailOnGameNightInvitation = emailOnGameNightInvitation,
            PushOnGameNightInvitation = pushOnGameNightInvitation,
            EmailOnGameNightReminder = emailOnGameNightReminder,
            PushOnGameNightReminder = pushOnGameNightReminder,
            SlackEnabled = slackEnabled,
            SlackOnDocumentReady = slackOnDocumentReady,
            SlackOnDocumentFailed = slackOnDocumentFailed,
            SlackOnRetryAvailable = slackOnRetryAvailable,
            SlackOnGameNightInvitation = slackOnGameNightInvitation,
            SlackOnGameNightReminder = slackOnGameNightReminder,
            SlackOnShareRequestCreated = slackOnShareRequestCreated,
            SlackOnShareRequestApproved = slackOnShareRequestApproved,
            SlackOnBadgeEarned = slackOnBadgeEarned
        };
    }

    public void UpdateEmailPreferences(bool onReady, bool onFailed, bool onRetry)
    {
        EmailOnDocumentReady = onReady;
        EmailOnDocumentFailed = onFailed;
        EmailOnRetryAvailable = onRetry;
    }

    public void UpdatePushPreferences(bool onReady, bool onFailed, bool onRetry)
    {
        PushOnDocumentReady = onReady;
        PushOnDocumentFailed = onFailed;
        PushOnRetryAvailable = onRetry;
    }

    public void UpdatePushSubscription(string? endpoint, string? p256dhKey, string? authKey)
    {
        PushEndpoint = endpoint;
        PushP256dhKey = p256dhKey;
        PushAuthKey = authKey;
    }

    public void ClearPushSubscription()
    {
        PushEndpoint = null;
        PushP256dhKey = null;
        PushAuthKey = null;
    }

    public bool HasPushSubscription => !string.IsNullOrEmpty(PushEndpoint);

    public void UpdateGameNightPreferences(
        bool inAppOnInvitation, bool emailOnInvitation, bool pushOnInvitation,
        bool emailOnReminder, bool pushOnReminder)
    {
        InAppOnGameNightInvitation = inAppOnInvitation;
        EmailOnGameNightInvitation = emailOnInvitation;
        PushOnGameNightInvitation = pushOnInvitation;
        EmailOnGameNightReminder = emailOnReminder;
        PushOnGameNightReminder = pushOnReminder;
    }

    public void UpdateInAppPreferences(bool onReady, bool onFailed, bool onRetry)
    {
        InAppOnDocumentReady = onReady;
        InAppOnDocumentFailed = onFailed;
        InAppOnRetryAvailable = onRetry;
    }

    public void UpdateSlackPreferences(bool enabled, bool onDocumentReady, bool onDocumentFailed,
        bool onRetryAvailable, bool onGameNightInvitation, bool onGameNightReminder,
        bool onShareRequestCreated, bool onShareRequestApproved, bool onBadgeEarned)
    {
        SlackEnabled = enabled;
        SlackOnDocumentReady = onDocumentReady;
        SlackOnDocumentFailed = onDocumentFailed;
        SlackOnRetryAvailable = onRetryAvailable;
        SlackOnGameNightInvitation = onGameNightInvitation;
        SlackOnGameNightReminder = onGameNightReminder;
        SlackOnShareRequestCreated = onShareRequestCreated;
        SlackOnShareRequestApproved = onShareRequestApproved;
        SlackOnBadgeEarned = onBadgeEarned;
    }

    public void UpdateAllPreferences(
        bool emailReady, bool emailFailed, bool emailRetry,
        bool pushReady, bool pushFailed, bool pushRetry,
        bool inAppReady, bool inAppFailed, bool inAppRetry)
    {
        UpdateEmailPreferences(emailReady, emailFailed, emailRetry);
        UpdatePushPreferences(pushReady, pushFailed, pushRetry);
        UpdateInAppPreferences(inAppReady, inAppFailed, inAppRetry);
    }
}
