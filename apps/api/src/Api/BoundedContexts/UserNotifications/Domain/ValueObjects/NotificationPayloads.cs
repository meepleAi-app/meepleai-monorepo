using System.Text.Json;
using System.Text.Json.Serialization;

namespace Api.BoundedContexts.UserNotifications.Domain.ValueObjects;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
[JsonDerivedType(typeof(ShareRequestPayload), "ShareRequestPayload")]
[JsonDerivedType(typeof(GameNightPayload), "GameNightPayload")]
[JsonDerivedType(typeof(PdfProcessingPayload), "PdfProcessingPayload")]
[JsonDerivedType(typeof(BadgePayload), "BadgePayload")]
[JsonDerivedType(typeof(GenericPayload), "GenericPayload")]
public interface INotificationPayload;

public record ShareRequestPayload(
    Guid ShareRequestId,
    string RequesterName,
    string GameTitle,
    string? GameImageUrl,
    string Status = "created") : INotificationPayload;

public record GameNightPayload(
    Guid GameNightId,
    string Title,
    DateTime ScheduledAt,
    string OrganizerName,
    bool IsCancelled = false) : INotificationPayload;

public record PdfProcessingPayload(
    Guid PdfId,
    string FileName,
    string Status) : INotificationPayload;

public record BadgePayload(
    Guid BadgeId,
    string BadgeName,
    string Description) : INotificationPayload;

public record GenericPayload(
    string Title,
    string Body) : INotificationPayload;

/// <summary>
/// The in-app text of a notification (#5111): the words its producer put in the payload. NotificationDispatcher used
/// <c>payload.ToString()</c>, the record's own representation (<c>ShareRequestPayload { ShareRequestId = … }</c>), and
/// the notification list shows that text as it is. Nothing is written here that the producer did not write: the
/// title of an in-app notification comes from its type, this is the line under it.
/// </summary>
public static class NotificationPayloadText
{
    public static string Of(INotificationPayload payload) => payload switch
    {
        ShareRequestPayload p => p.GameTitle,
        GameNightPayload p => p.Title,
        PdfProcessingPayload p => p.FileName,
        BadgePayload p => $"{p.BadgeName} — {p.Description}",
        GenericPayload p => p.Body,
        _ => throw new ArgumentOutOfRangeException(
            nameof(payload), payload?.GetType().Name, "A notification payload with no in-app text"),
    };
}

public static class NotificationPayloadSerializer
{
    public static JsonSerializerOptions CreateOptions()
    {
        return new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false,
            // #3057: payloads are persisted in a jsonb column, and Postgres reorders object keys
            // by (length, then bytewise). For GenericPayload that moves the "$type" discriminator
            // out of first position ({"body":..,"$type":..,"title":..}), which STJ polymorphic
            // deserialization rejects by default. Tolerate the discriminator at any position so
            // round-tripping through jsonb never throws (net9 feature).
            AllowOutOfOrderMetadataProperties = true
        };
    }
}
