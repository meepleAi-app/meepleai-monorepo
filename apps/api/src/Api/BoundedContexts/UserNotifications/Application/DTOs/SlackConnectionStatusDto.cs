namespace Api.BoundedContexts.UserNotifications.Application.DTOs;

/// <summary>
/// DTO representing the Slack connection status for a user.
/// </summary>
internal record SlackConnectionStatusDto(
    bool IsConnected,
    string? SlackTeamName,
    string? SlackUserId,
    DateTime? ConnectedAt);
