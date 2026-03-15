using Api.BoundedContexts.UserNotifications.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.UserNotifications.Domain.ValueObjects;

/// <summary>
/// Tests for the NotificationQueueStatus value object.
/// </summary>
[Trait("Category", "Unit")]
public sealed class NotificationQueueStatusTests
{
    #region Static Instance Tests

    [Fact]
    public void StaticInstances_HaveCorrectValues()
    {
        // Assert
        NotificationQueueStatus.Pending.Value.Should().Be("pending");
        NotificationQueueStatus.Processing.Value.Should().Be("processing");
        NotificationQueueStatus.Sent.Value.Should().Be("sent");
        NotificationQueueStatus.Failed.Value.Should().Be("failed");
        NotificationQueueStatus.DeadLetter.Value.Should().Be("dead_letter");
    }

    #endregion

    #region FromString Tests

    [Theory]
    [InlineData("pending")]
    [InlineData("PENDING")]
    [InlineData("Pending")]
    public void FromString_WithPending_ReturnsSameInstance(string value)
    {
        // Act
        var result = NotificationQueueStatus.FromString(value);

        // Assert
        result.Should().Be(NotificationQueueStatus.Pending);
    }

    [Theory]
    [InlineData("processing")]
    [InlineData("PROCESSING")]
    [InlineData("Processing")]
    public void FromString_WithProcessing_ReturnsSameInstance(string value)
    {
        // Act
        var result = NotificationQueueStatus.FromString(value);

        // Assert
        result.Should().Be(NotificationQueueStatus.Processing);
    }

    [Theory]
    [InlineData("sent")]
    [InlineData("SENT")]
    [InlineData("Sent")]
    public void FromString_WithSent_ReturnsSameInstance(string value)
    {
        // Act
        var result = NotificationQueueStatus.FromString(value);

        // Assert
        result.Should().Be(NotificationQueueStatus.Sent);
    }

    [Theory]
    [InlineData("failed")]
    [InlineData("FAILED")]
    [InlineData("Failed")]
    public void FromString_WithFailed_ReturnsSameInstance(string value)
    {
        // Act
        var result = NotificationQueueStatus.FromString(value);

        // Assert
        result.Should().Be(NotificationQueueStatus.Failed);
    }

    [Theory]
    [InlineData("dead_letter")]
    [InlineData("DEAD_LETTER")]
    [InlineData("Dead_Letter")]
    public void FromString_WithDeadLetter_ReturnsSameInstance(string value)
    {
        // Act
        var result = NotificationQueueStatus.FromString(value);

        // Assert
        result.Should().Be(NotificationQueueStatus.DeadLetter);
    }

    [Theory]
    [InlineData("unknown")]
    [InlineData("invalid_status")]
    [InlineData("")]
    [InlineData("queued")]
    public void FromString_WithUnknownStatus_ThrowsArgumentException(string value)
    {
        // Act
        var action = () => NotificationQueueStatus.FromString(value);

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithMessage($"Unknown notification queue status: {value}*");
    }

    #endregion

    #region Equality Tests

    [Fact]
    public void Equals_WithSameStatus_ReturnsTrue()
    {
        // Arrange
        var status1 = NotificationQueueStatus.Pending;
        var status2 = NotificationQueueStatus.Pending;

        // Act & Assert
        status1.Should().Be(status2);
    }

    [Fact]
    public void Equals_WithDifferentStatuses_ReturnsFalse()
    {
        // Arrange
        var status1 = NotificationQueueStatus.Pending;
        var status2 = NotificationQueueStatus.Sent;

        // Act & Assert
        status1.Should().NotBe(status2);
    }

    [Fact]
    public void GetHashCode_SameStatuses_ReturnsSameHash()
    {
        // Arrange
        var status1 = NotificationQueueStatus.Failed;
        var status2 = NotificationQueueStatus.Failed;

        // Act & Assert
        status1.GetHashCode().Should().Be(status2.GetHashCode());
    }

    [Fact]
    public void FromString_ReturnsSameReferenceAsStaticInstance()
    {
        // Act
        var fromString = NotificationQueueStatus.FromString("pending");

        // Assert
        fromString.Should().BeSameAs(NotificationQueueStatus.Pending);
    }

    #endregion

    #region ToString Tests

    [Fact]
    public void ToString_ReturnsValue()
    {
        // Assert
        NotificationQueueStatus.Pending.ToString().Should().Be("pending");
        NotificationQueueStatus.Processing.ToString().Should().Be("processing");
        NotificationQueueStatus.Sent.ToString().Should().Be("sent");
        NotificationQueueStatus.Failed.ToString().Should().Be("failed");
        NotificationQueueStatus.DeadLetter.ToString().Should().Be("dead_letter");
    }

    #endregion
}
