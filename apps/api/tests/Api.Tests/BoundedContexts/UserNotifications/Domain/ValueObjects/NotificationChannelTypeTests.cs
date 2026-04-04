using Api.BoundedContexts.UserNotifications.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.UserNotifications.Domain.ValueObjects;

/// <summary>
/// Tests for the NotificationChannelType value object.
/// </summary>
[Trait("Category", "Unit")]
public sealed class NotificationChannelTypeTests
{
    #region Static Instance Tests

    [Fact]
    public void StaticInstances_HaveCorrectValues()
    {
        // Assert
        NotificationChannelType.Email.Value.Should().Be("email");
        NotificationChannelType.SlackUser.Value.Should().Be("slack_user");
        NotificationChannelType.SlackTeam.Value.Should().Be("slack_team");
    }

    #endregion

    #region FromString Tests

    [Theory]
    [InlineData("email")]
    [InlineData("EMAIL")]
    [InlineData("Email")]
    public void FromString_WithEmail_ReturnsSameInstance(string value)
    {
        // Act
        var result = NotificationChannelType.FromString(value);

        // Assert
        result.Should().Be(NotificationChannelType.Email);
    }

    [Theory]
    [InlineData("slack_user")]
    [InlineData("SLACK_USER")]
    [InlineData("Slack_User")]
    public void FromString_WithSlackUser_ReturnsSameInstance(string value)
    {
        // Act
        var result = NotificationChannelType.FromString(value);

        // Assert
        result.Should().Be(NotificationChannelType.SlackUser);
    }

    [Theory]
    [InlineData("slack_team")]
    [InlineData("SLACK_TEAM")]
    [InlineData("Slack_Team")]
    public void FromString_WithSlackTeam_ReturnsSameInstance(string value)
    {
        // Act
        var result = NotificationChannelType.FromString(value);

        // Assert
        result.Should().Be(NotificationChannelType.SlackTeam);
    }

    [Theory]
    [InlineData("unknown")]
    [InlineData("invalid_channel")]
    [InlineData("")]
    [InlineData("sms")]
    public void FromString_WithUnknownType_ThrowsArgumentException(string value)
    {
        // Act
        var action = () => NotificationChannelType.FromString(value);

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithMessage($"Unknown notification channel type: {value}*");
    }

    #endregion

    #region Equality Tests

    [Fact]
    public void Equals_WithSameType_ReturnsTrue()
    {
        // Arrange
        var type1 = NotificationChannelType.Email;
        var type2 = NotificationChannelType.Email;

        // Act & Assert
        type1.Should().Be(type2);
    }

    [Fact]
    public void Equals_WithDifferentTypes_ReturnsFalse()
    {
        // Arrange
        var type1 = NotificationChannelType.Email;
        var type2 = NotificationChannelType.SlackUser;

        // Act & Assert
        type1.Should().NotBe(type2);
    }

    [Fact]
    public void GetHashCode_SameTypes_ReturnsSameHash()
    {
        // Arrange
        var type1 = NotificationChannelType.SlackTeam;
        var type2 = NotificationChannelType.SlackTeam;

        // Act & Assert
        type1.GetHashCode().Should().Be(type2.GetHashCode());
    }

    [Fact]
    public void FromString_ReturnsSameReferenceAsStaticInstance()
    {
        // Act
        var fromString = NotificationChannelType.FromString("email");

        // Assert
        fromString.Should().BeSameAs(NotificationChannelType.Email);
    }

    #endregion

    #region ToString Tests

    [Fact]
    public void ToString_ReturnsValue()
    {
        // Assert
        NotificationChannelType.Email.ToString().Should().Be("email");
        NotificationChannelType.SlackUser.ToString().Should().Be("slack_user");
        NotificationChannelType.SlackTeam.ToString().Should().Be("slack_team");
    }

    #endregion
}
