using System.Text.Json;
using Api.BoundedContexts.UserNotifications.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.UserNotifications.Domain.ValueObjects;

/// <summary>
/// Tests for polymorphic JSON serialization of INotificationPayload.
/// Verifies that the $type discriminator is correctly emitted and that
/// round-trip serialization preserves concrete types and all field values.
/// </summary>
[Trait("Category", "Unit")]
public sealed class NotificationPayloadSerializationTests
{
    private static readonly JsonSerializerOptions Options =
        NotificationPayloadSerializer.CreateOptions();

    #region ShareRequestPayload Tests

    [Fact]
    public void Serialize_ShareRequestPayload_IncludesTypeDiscriminator()
    {
        // Arrange
        var payload = new ShareRequestPayload(
            Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"),
            "Alice",
            "Catan",
            "https://example.com/catan.jpg");

        // Act
        var json = JsonSerializer.Serialize<INotificationPayload>(payload, Options);

        // Assert
        json.Should().Contain("\"$type\":\"ShareRequestPayload\"");
    }

    [Fact]
    public void RoundTrip_ShareRequestPayload_PreservesAllFields()
    {
        // Arrange
        var id = Guid.NewGuid();
        var original = new ShareRequestPayload(id, "Bob", "Wingspan", null);

        // Act
        var json = JsonSerializer.Serialize<INotificationPayload>(original, Options);
        var deserialized = JsonSerializer.Deserialize<INotificationPayload>(json, Options);

        // Assert
        deserialized.Should().BeOfType<ShareRequestPayload>();
        var result = (ShareRequestPayload)deserialized!;
        result.ShareRequestId.Should().Be(id);
        result.RequesterName.Should().Be("Bob");
        result.GameTitle.Should().Be("Wingspan");
        result.GameImageUrl.Should().BeNull();
    }

    #endregion

    #region GameNightPayload Tests

    [Fact]
    public void RoundTrip_GameNightPayload_PreservesAllFieldsIncludingDateTime()
    {
        // Arrange
        var id = Guid.NewGuid();
        var scheduledAt = new DateTime(2026, 6, 15, 19, 30, 0, DateTimeKind.Utc);
        var original = new GameNightPayload(id, "Friday Night Games", scheduledAt, "Charlie");

        // Act
        var json = JsonSerializer.Serialize<INotificationPayload>(original, Options);
        var deserialized = JsonSerializer.Deserialize<INotificationPayload>(json, Options);

        // Assert
        deserialized.Should().BeOfType<GameNightPayload>();
        var result = (GameNightPayload)deserialized!;
        result.GameNightId.Should().Be(id);
        result.Title.Should().Be("Friday Night Games");
        result.ScheduledAt.Should().Be(scheduledAt);
        result.OrganizerName.Should().Be("Charlie");
    }

    #endregion

    #region PdfProcessingPayload Tests

    [Fact]
    public void RoundTrip_PdfProcessingPayload_PreservesAllFields()
    {
        // Arrange
        var id = Guid.NewGuid();
        var original = new PdfProcessingPayload(id, "rules.pdf", "Indexed");

        // Act
        var json = JsonSerializer.Serialize<INotificationPayload>(original, Options);
        var deserialized = JsonSerializer.Deserialize<INotificationPayload>(json, Options);

        // Assert
        deserialized.Should().BeOfType<PdfProcessingPayload>();
        var result = (PdfProcessingPayload)deserialized!;
        result.PdfId.Should().Be(id);
        result.FileName.Should().Be("rules.pdf");
        result.Status.Should().Be("Indexed");
    }

    #endregion

    #region BadgePayload Tests

    [Fact]
    public void RoundTrip_BadgePayload_PreservesAllFields()
    {
        // Arrange
        var id = Guid.NewGuid();
        var original = new BadgePayload(id, "First Upload", "Uploaded your first PDF");

        // Act
        var json = JsonSerializer.Serialize<INotificationPayload>(original, Options);
        var deserialized = JsonSerializer.Deserialize<INotificationPayload>(json, Options);

        // Assert
        deserialized.Should().BeOfType<BadgePayload>();
        var result = (BadgePayload)deserialized!;
        result.BadgeId.Should().Be(id);
        result.BadgeName.Should().Be("First Upload");
        result.Description.Should().Be("Uploaded your first PDF");
    }

    #endregion

    #region GenericPayload Tests

    [Fact]
    public void RoundTrip_GenericPayload_PreservesAllFields()
    {
        // Arrange
        var original = new GenericPayload("System Update", "Maintenance window at 2 AM");

        // Act
        var json = JsonSerializer.Serialize<INotificationPayload>(original, Options);
        var deserialized = JsonSerializer.Deserialize<INotificationPayload>(json, Options);

        // Assert
        deserialized.Should().BeOfType<GenericPayload>();
        var result = (GenericPayload)deserialized!;
        result.Title.Should().Be("System Update");
        result.Body.Should().Be("Maintenance window at 2 AM");
    }

    #endregion

    #region Type Discriminator Behavior Tests

    [Fact]
    public void Serialize_WithoutInterfaceTypeParam_DoesNotIncludeTypeDiscriminator()
    {
        // Arrange
        // This test documents that serializing as the concrete type (not INotificationPayload)
        // will NOT include the $type discriminator — a common pitfall.
        var payload = new ShareRequestPayload(
            Guid.NewGuid(),
            "Alice",
            "Catan",
            null);

        // Act — serialize as concrete type, not as INotificationPayload
        var json = JsonSerializer.Serialize(payload, Options);

        // Assert
        json.Should().NotContain("\"$type\"");
    }

    #endregion
}
