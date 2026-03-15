using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.UserNotifications.Application.Commands;

/// <summary>
/// Command to update Slack notification preferences for a user.
/// Controls which notification types are sent via Slack.
/// </summary>
internal record UpdateSlackPreferencesCommand(
    Guid UserId,
    bool SlackEnabled,
    bool SlackOnDocumentReady,
    bool SlackOnDocumentFailed,
    bool SlackOnRetryAvailable,
    bool SlackOnGameNightInvitation,
    bool SlackOnGameNightReminder,
    bool SlackOnShareRequestCreated,
    bool SlackOnShareRequestApproved,
    bool SlackOnBadgeEarned
) : ICommand;
