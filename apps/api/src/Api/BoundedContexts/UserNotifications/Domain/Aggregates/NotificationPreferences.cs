using Api.SharedKernel.Domain.Entities;
using TimeZoneConverter;

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

    // #535 ME-M3.3: opt-in email when a mechanic card is suppressed (admin-facing). Default off.
    public bool EmailOnCardSuppressed { get; private set; }

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

    // Quiet hours - ADR-076 (#2383 follow-up)
    /// <summary>
    /// IANA timezone identifier (e.g. "Europe/Rome", "America/New_York") used to
    /// compute local time for quiet-hours gating. Defaults to "UTC".
    /// Resolved cross-platform via <see cref="TimeZoneConverter"/> on Linux hosts.
    /// </summary>
    public string TimeZone { get; private set; } = "UTC";

    /// <summary>
    /// Optional start of daily quiet-hours window (local time per <see cref="TimeZone"/>).
    /// When set together with <see cref="QuietHoursEnd"/>, email + Slack channels
    /// are gated server-side. In-app channels are NEVER suppressed (records persisted).
    /// </summary>
    public TimeOnly? QuietHoursStart { get; private set; }

    /// <summary>
    /// Optional end of daily quiet-hours window. May be earlier than <see cref="QuietHoursStart"/>
    /// (window crosses midnight, e.g. 22:00 → 08:00).
    /// </summary>
    public TimeOnly? QuietHoursEnd { get; private set; }

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
        bool slackOnShareRequestApproved = true, bool slackOnBadgeEarned = true,
        string timeZone = "UTC", TimeOnly? quietHoursStart = null, TimeOnly? quietHoursEnd = null,
        bool emailOnCardSuppressed = false)
    {
        return new NotificationPreferences
        {
            Id = id,
            UserId = userId,
            TimeZone = string.IsNullOrWhiteSpace(timeZone) ? "UTC" : timeZone,
            QuietHoursStart = quietHoursStart,
            QuietHoursEnd = quietHoursEnd,
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
            SlackOnBadgeEarned = slackOnBadgeEarned,
            EmailOnCardSuppressed = emailOnCardSuppressed
        };
    }

    public void UpdateEmailPreferences(bool onReady, bool onFailed, bool onRetry)
    {
        EmailOnDocumentReady = onReady;
        EmailOnDocumentFailed = onFailed;
        EmailOnRetryAvailable = onRetry;
    }

    /// <summary>#535: toggle the per-admin card-suppression email opt-in.</summary>
    public void UpdateCardSuppressionEmailPreference(bool email)
    {
        EmailOnCardSuppressed = email;
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

    /// <summary>
    /// Updates timezone + quiet-hours window. Per ADR-076, gating applies only to
    /// email + Slack channels server-side. In-app records persist regardless.
    /// Pass null start/end to disable quiet hours.
    /// </summary>
    public void UpdateQuietHours(string timeZone, TimeOnly? start, TimeOnly? end)
    {
        if (string.IsNullOrWhiteSpace(timeZone))
            throw new ArgumentException("Time zone cannot be empty", nameof(timeZone));

        if (start.HasValue != end.HasValue)
            throw new ArgumentException(
                "Quiet hours start and end must both be set or both be null",
                nameof(start));

        TimeZone = timeZone;
        QuietHoursStart = start;
        QuietHoursEnd = end;
    }

    /// <summary>
    /// Returns true if the given UTC timestamp falls inside the user's quiet-hours
    /// window. Window may cross midnight (start > end, e.g. 22:00 → 08:00 local).
    /// Returns false if quiet hours not configured.
    /// </summary>
    public bool IsQuietHoursActive(DateTimeOffset utcNow)
    {
        if (!QuietHoursStart.HasValue || !QuietHoursEnd.HasValue)
            return false;

        var tzInfo = TZConvert.GetTimeZoneInfo(TimeZone);
        var localNow = TimeOnly.FromDateTime(
            TimeZoneInfo.ConvertTimeFromUtc(utcNow.UtcDateTime, tzInfo));

        var start = QuietHoursStart.Value;
        var end = QuietHoursEnd.Value;

        // Window crosses midnight (e.g. 22:00 → 08:00): active if localNow >= start OR localNow < end
        if (start > end)
            return localNow >= start || localNow < end;

        // Normal window (e.g. 09:00 → 17:00): active if start <= localNow < end
        return localNow >= start && localNow < end;
    }
}
