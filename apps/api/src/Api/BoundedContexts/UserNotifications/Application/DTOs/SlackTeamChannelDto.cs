namespace Api.BoundedContexts.UserNotifications.Application.DTOs;

/// <summary>
/// DTO representing a Slack team channel configuration for admin management.
/// </summary>
internal record SlackTeamChannelDto(
    Guid Id,
    string ChannelName,
    string WebhookUrl,
    string[] NotificationTypes,
    bool IsEnabled,
    bool OverridesDefault);
