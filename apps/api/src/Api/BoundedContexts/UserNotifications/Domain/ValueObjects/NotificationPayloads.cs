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
    string? GameImageUrl) : INotificationPayload;

public record GameNightPayload(
    Guid GameNightId,
    string Title,
    DateTime ScheduledAt,
    string OrganizerName) : INotificationPayload;

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
            WriteIndented = false
        };
    }
}
