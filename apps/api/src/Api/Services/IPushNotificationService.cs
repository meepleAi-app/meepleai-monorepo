namespace Api.Services;

/// <summary>
/// Service for sending Web Push notifications.
/// Issue #4416: Push notifications via Service Worker.
/// </summary>
internal interface IPushNotificationService
{
    Task SendPushNotificationAsync(
        string endpoint,
        string p256dhKey,
        string authKey,
        string title,
        string body,
        string? url = null,
        CancellationToken ct = default);
}
