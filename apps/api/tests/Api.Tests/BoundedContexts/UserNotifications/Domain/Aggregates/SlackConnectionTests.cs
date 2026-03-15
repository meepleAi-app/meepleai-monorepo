using Api.BoundedContexts.UserNotifications.Domain.Aggregates;
using Xunit;

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
        Assert.NotEqual(Guid.Empty, connection.Id);
        Assert.Equal(DefaultUserId, connection.UserId);
        Assert.Equal("U01ABCDEF", connection.SlackUserId);
        Assert.Equal("T01ABCDEF", connection.SlackTeamId);
        Assert.Equal("Test Workspace", connection.SlackTeamName);
        Assert.Equal("xoxb-test-token", connection.BotAccessToken);
        Assert.Equal("D01ABCDEF", connection.DmChannelId);
        Assert.True(connection.IsActive);
        Assert.Null(connection.DisconnectedAt);
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
        Assert.False(connection.IsActive);
        Assert.Equal(disconnectedAt, connection.DisconnectedAt);
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
        Assert.True(connection.IsActive);
        Assert.Equal("xoxb-new-token", connection.BotAccessToken);
        Assert.Equal("D99NEWCHAN", connection.DmChannelId);
        Assert.Null(connection.DisconnectedAt);
    }

    [Fact]
    public void Deactivate_SetsIsActiveFalseWithoutTimestamp()
    {
        // Arrange
        var connection = CreateDefault();

        // Act
        connection.Deactivate();

        // Assert
        Assert.False(connection.IsActive);
        Assert.Null(connection.DisconnectedAt);
    }
}
