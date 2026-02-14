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

    // PDF Processing - In-App
    public bool InAppOnDocumentReady { get; private set; } = true;
    public bool InAppOnDocumentFailed { get; private set; } = true;
    public bool InAppOnRetryAvailable { get; private set; } = true;

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
        bool inAppReady, bool inAppFailed, bool inAppRetry)
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
            InAppOnRetryAvailable = inAppRetry
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

    public void UpdateInAppPreferences(bool onReady, bool onFailed, bool onRetry)
    {
        InAppOnDocumentReady = onReady;
        InAppOnDocumentFailed = onFailed;
        InAppOnRetryAvailable = onRetry;
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
