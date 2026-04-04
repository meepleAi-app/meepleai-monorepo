namespace Api.BoundedContexts.UserNotifications.Application.DTOs;

/// <summary>
/// DTO representing a Slack connection for admin viewing.
/// Excludes sensitive bot access token.
/// </summary>
internal record SlackConnectionDto(
    Guid Id,
    Guid UserId,
    string SlackUserId,
    string SlackTeamId,
    string SlackTeamName,
    string DmChannelId,
    bool IsActive,
    DateTime ConnectedAt,
    DateTime? DisconnectedAt);
