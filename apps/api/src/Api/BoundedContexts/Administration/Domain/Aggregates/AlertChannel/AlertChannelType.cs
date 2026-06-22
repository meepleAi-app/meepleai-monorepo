#pragma warning disable MA0048 // File name must match type name - enum + helpers kept together
namespace Api.BoundedContexts.Administration.Domain.Aggregates.AlertChannels;

/// <summary>
/// Supported notification channels for alerts (Issue #1840 SP5 F4-C7).
///
/// <para>Scope intentionally limited to the two channels confirmed in the
/// spec D1 decision: Email + Slack. PagerDuty was explicitly removed from
/// the #1840 scope and the legacy <c>PagerDutyAlertChannel</c> service
/// remains in place untouched for OPS-07 backwards-compatibility.</para>
/// </summary>
internal enum AlertChannelType
{
    Email = 0,
    Slack = 1,
}

internal static class AlertChannelTypeExtensions
{
    public static string ToWireValue(this AlertChannelType type) => type switch
    {
        AlertChannelType.Email => "email",
        AlertChannelType.Slack => "slack",
        _ => throw new ArgumentOutOfRangeException(nameof(type)),
    };

    public static AlertChannelType FromWireValue(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return value.ToLowerInvariant() switch
        {
            "email" => AlertChannelType.Email,
            "slack" => AlertChannelType.Slack,
            _ => throw new ArgumentException($"Unknown alert channel type: '{value}'", nameof(value)),
        };
    }
}
