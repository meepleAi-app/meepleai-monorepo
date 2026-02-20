using Api.BoundedContexts.SessionTracking.Domain.Services;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.SessionTracking.Domain;

[Trait("Category", "Unit")]
[Trait("BoundedContext", "SessionTracking")]
public class EventVisibilityTests
{
    [Fact]
    public void Public_IsPublicTrue_NoTargetUser()
    {
        // Act
        var visibility = EventVisibility.Public;

        // Assert
        visibility.IsPublic.Should().BeTrue();
        visibility.TargetUserId.Should().BeNull();
    }

    [Fact]
    public void PrivateTo_IsPublicFalse_HasTargetUser()
    {
        // Arrange
        var userId = Guid.NewGuid();

        // Act
        var visibility = EventVisibility.PrivateTo(userId);

        // Assert
        visibility.IsPublic.Should().BeFalse();
        visibility.TargetUserId.Should().Be(userId);
    }

    [Fact]
    public void Default_IsPublicFalse_NoTargetUser()
    {
        // Act
        var visibility = default(EventVisibility);

        // Assert
        visibility.IsPublic.Should().BeFalse();
        visibility.TargetUserId.Should().BeNull();
    }

    [Fact]
    public void SseEventEnvelope_RequiredFields_AreSet()
    {
        // Arrange & Act
        var envelope = new SseEventEnvelope
        {
            Id = "test-123",
            EventType = "session:score",
            Data = new { Score = 42 }
        };

        // Assert
        envelope.Id.Should().Be("test-123");
        envelope.EventType.Should().Be("session:score");
        envelope.Data.Should().NotBeNull();
        envelope.Timestamp.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }
}
