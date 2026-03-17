using System.Globalization;
using Api.BoundedContexts.UserNotifications.Domain.ValueObjects;

namespace Api.BoundedContexts.UserNotifications.Infrastructure.Slack;

/// <summary>
/// Block Kit builder for game night notifications.
/// Produces RSVP Yes/No/Maybe buttons with block_id format: gn:{GameNightId}:{unix_timestamp}.
/// </summary>
internal sealed class GameNightSlackBuilder : ISlackMessageBuilder
{
    private readonly TimeProvider _timeProvider;

    public GameNightSlackBuilder(TimeProvider timeProvider)
    {
        _timeProvider = timeProvider;
    }

    public bool CanHandle(NotificationType type)
    {
        return type == NotificationType.GameNightInvitation
            || type == NotificationType.GameNightRsvpReceived
            || type == NotificationType.GameNightReminder24h
            || type == NotificationType.GameNightReminder1h
            || type == NotificationType.GameNightCancelled;
    }

    public object BuildMessage(INotificationPayload payload, string? deepLinkPath)
    {
        if (payload is not GameNightPayload gn)
        {
            throw new ArgumentException($"Expected {nameof(GameNightPayload)} but received {payload.GetType().Name}", nameof(payload));
        }

        var timestamp = _timeProvider.GetUtcNow().ToUnixTimeSeconds();
        var blockId = $"gn:{gn.GameNightId}:{timestamp}";
        var scheduledDate = gn.ScheduledAt.ToString("dddd d MMMM yyyy, HH:mm", CultureInfo.InvariantCulture);

        var blocks = new List<object>
        {
            new
            {
                type = "header",
                text = new { type = "plain_text", text = $"\ud83c\udfb2 {gn.Title}", emoji = true }
            },
            new
            {
                type = "section",
                text = new
                {
                    type = "mrkdwn",
                    text = $"\ud83d\udcc5 *Data*: {scheduledDate}\n\ud83d\udc64 *Organizzatore*: {gn.OrganizerName}"
                }
            },
            new
            {
                type = "actions",
                block_id = blockId,
                elements = new object[]
                {
                    new
                    {
                        type = "button",
                        text = new { type = "plain_text", text = "\u2705 Partecipo", emoji = true },
                        style = "primary",
                        action_id = "game_night_rsvp_yes",
                        value = gn.GameNightId.ToString()
                    },
                    new
                    {
                        type = "button",
                        text = new { type = "plain_text", text = "\u274c Non partecipo", emoji = true },
                        style = "danger",
                        action_id = "game_night_rsvp_no",
                        value = gn.GameNightId.ToString()
                    },
                    new
                    {
                        type = "button",
                        text = new { type = "plain_text", text = "\ud83e\udd14 Forse", emoji = true },
                        action_id = "game_night_rsvp_maybe",
                        value = gn.GameNightId.ToString()
                    }
                }
            }
        };

        return new { blocks };
    }
}
