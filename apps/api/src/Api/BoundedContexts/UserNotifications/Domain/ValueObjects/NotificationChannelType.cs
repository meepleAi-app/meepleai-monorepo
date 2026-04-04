using Api.SharedKernel.Domain.ValueObjects;

namespace Api.BoundedContexts.UserNotifications.Domain.ValueObjects;

/// <summary>
/// Value object representing the delivery channel for a notification.
/// Defines how notifications reach users (email, Slack DM, Slack team channel).
/// </summary>
internal sealed class NotificationChannelType : ValueObject
{
    public string Value { get; }

    public static readonly NotificationChannelType Email = new("email");
    public static readonly NotificationChannelType SlackUser = new("slack_user");
    public static readonly NotificationChannelType SlackTeam = new("slack_team");

    private NotificationChannelType(string value)
    {
        Value = value;
    }

    /// <summary>
    /// Creates a NotificationChannelType from a string value.
    /// Validates against known channel types.
    /// </summary>
    public static NotificationChannelType FromString(string value)
    {
        var normalized = value.ToLowerInvariant();
        return normalized switch
        {
            "email" => Email,
            "slack_user" => SlackUser,
            "slack_team" => SlackTeam,
            _ => throw new ArgumentException($"Unknown notification channel type: {value}", nameof(value))
        };
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }

    public override string ToString() => Value;
}
