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
}
