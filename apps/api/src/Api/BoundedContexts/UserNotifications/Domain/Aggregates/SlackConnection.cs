using Api.SharedKernel.Domain.Entities;

namespace Api.BoundedContexts.UserNotifications.Domain.Aggregates;

/// <summary>
/// Aggregate root representing a user's Slack workspace connection.
/// Manages the OAuth lifecycle: connect, disconnect, reconnect, deactivate.
/// Bot access token encryption is handled by EF ValueConverter, not here.
/// </summary>
internal sealed class SlackConnection : AggregateRoot<Guid>
{
    public Guid UserId { get; private set; }
    public string SlackUserId { get; private set; }
    public string SlackTeamId { get; private set; }
    public string SlackTeamName { get; private set; }
    public string BotAccessToken { get; private set; }
    public string DmChannelId { get; private set; }
    public bool IsActive { get; private set; }
    public DateTime ConnectedAt { get; private set; }
    public DateTime? DisconnectedAt { get; private set; }

#pragma warning disable CS8618
    private SlackConnection() : base() { }
#pragma warning restore CS8618

    private SlackConnection(
        Guid id,
        Guid userId,
        string slackUserId,
        string slackTeamId,
        string slackTeamName,
        string botAccessToken,
        string dmChannelId,
        DateTime? connectedAt = null)
        : base(id)
    {
        UserId = userId;
        SlackUserId = !string.IsNullOrWhiteSpace(slackUserId)
            ? slackUserId
            : throw new ArgumentException("Slack user ID cannot be empty", nameof(slackUserId));
        SlackTeamId = !string.IsNullOrWhiteSpace(slackTeamId)
            ? slackTeamId
            : throw new ArgumentException("Slack team ID cannot be empty", nameof(slackTeamId));
        SlackTeamName = !string.IsNullOrWhiteSpace(slackTeamName)
            ? slackTeamName
            : throw new ArgumentException("Slack team name cannot be empty", nameof(slackTeamName));
        BotAccessToken = !string.IsNullOrWhiteSpace(botAccessToken)
            ? botAccessToken
            : throw new ArgumentException("Bot access token cannot be empty", nameof(botAccessToken));
        DmChannelId = !string.IsNullOrWhiteSpace(dmChannelId)
            ? dmChannelId
            : throw new ArgumentException("DM channel ID cannot be empty", nameof(dmChannelId));
        IsActive = true;
        ConnectedAt = connectedAt ?? DateTime.UtcNow;
        DisconnectedAt = null;
    }

    /// <summary>
    /// Factory method to create a new Slack connection.
    /// </summary>
    public static SlackConnection Create(
        Guid userId,
        string slackUserId,
        string slackTeamId,
        string slackTeamName,
        string botAccessToken,
        string dmChannelId,
        DateTime? connectedAt = null)
    {
        return new SlackConnection(
            Guid.NewGuid(),
            userId,
            slackUserId,
            slackTeamId,
            slackTeamName,
            botAccessToken,
            dmChannelId,
            connectedAt);
    }

    /// <summary>
    /// Disconnects the Slack connection with a recorded timestamp.
    /// Used when the user explicitly disconnects their workspace.
    /// </summary>
    public void Disconnect(DateTime disconnectedAt)
    {
        IsActive = false;
        DisconnectedAt = disconnectedAt;
    }

    /// <summary>
    /// Reconnects a previously disconnected Slack connection.
    /// Updates the bot access token and DM channel, clears disconnect timestamp.
    /// </summary>
    public void Reconnect(string newToken, string newDmChannelId)
    {
        BotAccessToken = !string.IsNullOrWhiteSpace(newToken)
            ? newToken
            : throw new ArgumentException("Bot access token cannot be empty", nameof(newToken));
        DmChannelId = !string.IsNullOrWhiteSpace(newDmChannelId)
            ? newDmChannelId
            : throw new ArgumentException("DM channel ID cannot be empty", nameof(newDmChannelId));
        IsActive = true;
        DisconnectedAt = null;
    }

    /// <summary>
    /// Deactivates the connection without recording a timestamp.
    /// Used for token revocation or administrative deactivation.
    /// </summary>
    public void Deactivate()
    {
        IsActive = false;
    }

    /// <summary>
    /// Reconstitutes the aggregate from persistence.
    /// Used by repository when mapping from database entity.
    /// </summary>
    internal static SlackConnection Reconstitute(
        Guid id,
        Guid userId,
        string slackUserId,
        string slackTeamId,
        string slackTeamName,
        string botAccessToken,
        string dmChannelId,
        bool isActive,
        DateTime connectedAt,
        DateTime? disconnectedAt)
    {
        var connection = new SlackConnection(
            id, userId, slackUserId, slackTeamId,
            slackTeamName, botAccessToken, dmChannelId);
        connection.IsActive = isActive;
        connection.ConnectedAt = connectedAt;
        connection.DisconnectedAt = disconnectedAt;
        return connection;
    }
}
