using Api.BoundedContexts.UserNotifications.Domain.Aggregates;
using Xunit;
using FluentAssertions;

namespace Api.Tests.BoundedContexts.UserNotifications.Domain.Aggregates;

[Trait("Category", "Unit")]
[Trait("BoundedContext", "UserNotifications")]
public class SlackConnectionTests
{
    private static readonly Guid DefaultUserId = Guid.NewGuid();

    private static SlackConnection CreateDefault(
        Guid? userId = null,
        string slackUserId = "U01ABCDEF",
        string slackTeamId = "T01ABCDEF",
        string slackTeamName = "Test Workspace",
        string botAccessToken = "xoxb-test-token",
        string dmChannelId = "D01ABCDEF")
    {
        return SlackConnection.Create(
            userId ?? DefaultUserId,
            slackUserId,
            slackTeamId,
            slackTeamName,
            botAccessToken,
            dmChannelId);
    }

    [Fact]
    public void Create_SetsAllFieldsCorrectly()
    {
        // Arrange & Act
        var connection = CreateDefault();

        // Assert
        connection.Id.Should().NotBe(Guid.Empty);
        connection.UserId.Should().Be(DefaultUserId);
        connection.SlackUserId.Should().Be("U01ABCDEF");
        connection.SlackTeamId.Should().Be("T01ABCDEF");
        connection.SlackTeamName.Should().Be("Test Workspace");
        connection.BotAccessToken.Should().Be("xoxb-test-token");
        connection.DmChannelId.Should().Be("D01ABCDEF");
        connection.IsActive.Should().BeTrue();
        connection.DisconnectedAt.Should().BeNull();
    }

    [Fact]
    public void Disconnect_SetsIsActiveFalseAndRecordsTimestamp()
    {
        // Arrange
        var connection = CreateDefault();
        var disconnectedAt = new DateTime(2026, 3, 15, 12, 0, 0, DateTimeKind.Utc);

        // Act
        connection.Disconnect(disconnectedAt);

        // Assert
        connection.IsActive.Should().BeFalse();
        connection.DisconnectedAt.Should().Be(disconnectedAt);
    }

    [Fact]
    public void Reconnect_AfterDisconnect_SetsIsActiveTrueAndUpdatesTokenAndChannel()
    {
        // Arrange
        var connection = CreateDefault();
        connection.Disconnect(DateTime.UtcNow);

        // Act
        connection.Reconnect("xoxb-new-token", "D99NEWCHAN");

        // Assert
        connection.IsActive.Should().BeTrue();
        connection.BotAccessToken.Should().Be("xoxb-new-token");
        connection.DmChannelId.Should().Be("D99NEWCHAN");
        connection.DisconnectedAt.Should().BeNull();
    }

    [Fact]
    public void Deactivate_SetsIsActiveFalseWithoutTimestamp()
    {
        // Arrange
        var connection = CreateDefault();

        // Act
        connection.Deactivate();

        // Assert
        connection.IsActive.Should().BeFalse();
        connection.DisconnectedAt.Should().BeNull();
    }
}
