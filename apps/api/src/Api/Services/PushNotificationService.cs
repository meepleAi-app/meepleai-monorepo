using System.Text.Json;
using WebPush;

namespace Api.Services;

/// <summary>
/// Web Push notification service using VAPID authentication.
/// Issue #4416: Push notifications via Service Worker.
/// </summary>
internal sealed class PushNotificationService : IPushNotificationService, IDisposable
{
    private readonly ILogger<PushNotificationService> _logger;
    private readonly WebPushClient _pushClient;
    private readonly VapidDetails? _vapidDetails;

    public PushNotificationService(IConfiguration configuration, ILogger<PushNotificationService> logger)
    {
        _logger = logger;
        _pushClient = new WebPushClient();

        var publicKey = configuration["Push:VapidPublicKey"];
        var privateKey = configuration["Push:VapidPrivateKey"];
        var subject = configuration["Push:VapidSubject"] ?? "mailto:admin@meepleai.dev";

        if (!string.IsNullOrEmpty(publicKey) && !string.IsNullOrEmpty(privateKey))
        {
            _vapidDetails = new VapidDetails(subject, publicKey, privateKey);
            _logger.LogInformation("Push notification service initialized with VAPID keys");
        }
        else
        {
            _logger.LogWarning("Push notification service initialized without VAPID keys - push notifications will not be sent");
        }
    }

    public async Task SendPushNotificationAsync(
        string endpoint,
        string p256dhKey,
        string authKey,
        string title,
        string body,
        string? url = null,
        CancellationToken ct = default)
    {
        if (_vapidDetails == null)
        {
            _logger.LogWarning("Cannot send push notification: VAPID keys not configured");
            return;
        }

        var subscription = new PushSubscription(endpoint, p256dhKey, authKey);

        var payload = JsonSerializer.Serialize(new
        {
            title,
            body,
            url = url ?? "/"
        });

        try
        {
            await _pushClient.SendNotificationAsync(subscription, payload, _vapidDetails, ct).ConfigureAwait(false);
            _logger.LogInformation("Push notification sent to endpoint {Endpoint}", endpoint[..Math.Min(50, endpoint.Length)]);
        }
        catch (WebPushException ex) when (ex.StatusCode == System.Net.HttpStatusCode.Gone)
        {
            _logger.LogWarning(ex, "Push subscription expired (410 Gone) for endpoint {Endpoint}", endpoint[..Math.Min(50, endpoint.Length)]);
            throw;
        }
#pragma warning disable CA1031
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send push notification to endpoint {Endpoint}", endpoint[..Math.Min(50, endpoint.Length)]);
        }
#pragma warning restore CA1031
    }

    public void Dispose()
    {
        _pushClient.Dispose();
    }
}
