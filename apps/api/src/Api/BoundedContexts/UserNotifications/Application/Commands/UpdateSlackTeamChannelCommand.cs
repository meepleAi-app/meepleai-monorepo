using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.UserNotifications.Application.Commands;

/// <summary>
/// Command to update a Slack team channel configuration.
/// Allows admin to override webhook URL, notification types, and enabled status.
/// </summary>
internal record UpdateSlackTeamChannelCommand(
    Guid Id,
    string? ChannelName,
    string? WebhookUrl,
    string[]? NotificationTypes,
    bool? IsEnabled,
    bool? OverridesDefault) : ICommand;
