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
