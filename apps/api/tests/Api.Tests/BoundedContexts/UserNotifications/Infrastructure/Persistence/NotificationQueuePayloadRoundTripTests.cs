using System.Text.Json;
using Api.BoundedContexts.UserNotifications.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.UserNotifications.Infrastructure.Persistence;

/// <summary>
/// Tests that verify the payload serialization round-trip behavior used by
/// NotificationQueueRepository.MapToPersistence (serialize) and MapToDomain (deserialize).
/// Ensures $type discriminator is correctly emitted when serializing as INotificationPayload
/// and that deserialization recovers the correct concrete type.
/// </summary>
[Trait("Category", "Unit")]
public sealed class NotificationQueuePayloadRoundTripTests
{
    private static readonly JsonSerializerOptions Options =
        NotificationPayloadSerializer.CreateOptions();

    [Fact]
    public void MapToPersistence_SerializesWithTypeDiscriminator()
    {
        // Arrange — simulate MapToPersistence: serialize as INotificationPayload
        INotificationPayload payload = new ShareRequestPayload(Guid.NewGuid(), "Mario", "Catan", null);

        // Act
        var json = JsonSerializer.Serialize<INotificationPayload>(payload, Options);

        // Assert
        json.Should().Contain("\"$type\"");
    }

    [Fact]
    public void MapToDomain_DeserializesToCorrectConcreteType()
    {
        // Arrange — simulate full round-trip: MapToPersistence then MapToDomain
        INotificationPayload payload = new GameNightPayload(Guid.NewGuid(), "Friday", DateTime.UtcNow, "Luigi");

        // Act
        var json = JsonSerializer.Serialize<INotificationPayload>(payload, Options);
        var result = JsonSerializer.Deserialize<INotificationPayload>(json, Options);

        // Assert
        result.Should().BeOfType<GameNightPayload>();
    }

    [Fact]
    public void Serialize_WithoutGenericTypeParam_DoesNotIncludeDiscriminator()
    {
        // Arrange — document pitfall: concrete-type serialization omits discriminator
        var payload = new ShareRequestPayload(Guid.NewGuid(), "Mario", "Catan", null);

        // Act — serialize as concrete type (not INotificationPayload)
        var json = JsonSerializer.Serialize(payload, Options);

        // Assert
        json.Should().NotContain("\"$type\"");
    }
}
