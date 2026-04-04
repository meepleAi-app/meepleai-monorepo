using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.UserNotifications.Application.Commands;

/// <summary>
/// Command to handle an incoming Slack interaction payload (button click, menu selection, etc.).
/// Contains the raw payload and Slack signing headers for validation.
/// </summary>
internal record HandleSlackInteractionCommand(
    string Payload,      // Raw JSON payload from Slack
    string Timestamp,    // X-Slack-Request-Timestamp header
    string Signature     // X-Slack-Signature header
) : ICommand<SlackInteractionResult>;

/// <summary>
/// Result of processing a Slack interaction.
/// Always returns HTTP 200 to Slack; Success indicates whether the action was processed.
/// </summary>
internal record SlackInteractionResult(bool Success, string? ResponseMessage = null);
